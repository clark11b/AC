using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Sequence;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// Returns all inventory, side slot items, items in side containers, and all wielded items.
        /// </summary>
        public List<WorldObject> GetAllPossessions()
        {
            var results = new List<WorldObject>();

            results.AddRange(Inventory.Values);

            foreach (var item in Inventory.Values)
            {
                if (item is Container container)
                    results.AddRange(container.Inventory.Values);
            }

            results.AddRange(EquippedObjects.Values);

            return results;
        }


        public int GetEncumbranceCapacity()
        {
            return int.MaxValue; // fix
            /*var encumbranceAgumentations = 0; // todo

            var strength = Attributes[PropertyAttribute.Strength].Current;

            return (int)((150 * strength) + (encumbranceAgumentations * 30 * strength));*/
        }

        public bool HasEnoughBurdenToAddToInventory(WorldObject worldObject)
        {
            return (EncumbranceVal + worldObject.EncumbranceVal <= GetEncumbranceCapacity());
        }


        /// <summary>
        /// If enough burden is available, this will try to add (via create) an item to the main pack. If the main pack is full, it will try to add it to the first side pack with room.
        /// </summary>
        public bool TryCreateInInventoryWithNetworking(WorldObject worldObject, int placementPosition = 0, bool limitToMainPackOnly = false)
        {
            return TryCreateInInventoryWithNetworking(worldObject, out _, placementPosition, limitToMainPackOnly);
        }

        /// <summary>
        /// If enough burden is available, this will try to add (via create) an item to the main pack. If the main pack is full, it will try to add it to the first side pack with room.
        /// </summary>
        public bool TryCreateInInventoryWithNetworking(WorldObject worldObject, out Container container, int placementPosition = 0, bool limitToMainPackOnly = false)
        {
            if (!TryAddToInventory(worldObject, out container, placementPosition, limitToMainPackOnly)) // We don't have enough burden available or no empty pack slot.
                return false;

            Session.Network.EnqueueSend(new GameMessageCreateObject(worldObject));

            if (worldObject is Container lootAsContainer)
                Session.Network.EnqueueSend(new GameEventViewContents(Session, lootAsContainer));

            Session.Network.EnqueueSend(
                new GameEventItemServerSaysContainId(Session, worldObject, container),
                new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

            if (worldObject.WeenieType == WeenieType.Coin)
                UpdateCoinValue();

            return true;
        }

        /// <summary>
        /// This method is used to remove X number of items from a stack.<para />
        /// If amount to remove is greater or equal to the current stacksize, the stack will be destroyed..
        /// </summary>
        public bool TryRemoveItemFromInventoryWithNetworkingWithDestroy(WorldObject worldObject, ushort amount)
        {
            if (amount >= (worldObject.StackSize ?? 1))
            {
                if (TryRemoveFromInventoryWithNetworking(worldObject))
                {
                    worldObject.Destroy();
                    return true;
                }

                return false;
            }

            worldObject.StackSize -= amount;

            Session.Network.EnqueueSend(new GameMessageSetStackSize(worldObject));

            EncumbranceVal = (EncumbranceVal - (worldObject.StackUnitEncumbrance * amount));
            Value = (Value - (worldObject.StackUnitValue * amount));

            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

            if (worldObject.WeenieType == WeenieType.Coin)
                UpdateCoinValue();

            return true;
        }

        /// <summary>
        /// If isFromMergeEvent is false, update messages will be sent for EncumbranceVal and if WeenieType is Coin, CoinValue will be updated and update messages will be sent for CoinValue.
        /// </summary>
        public bool TryRemoveFromInventoryWithNetworking(WorldObject worldObject, bool isFromMergeEvent = false)
        {
            if (TryRemoveFromInventory(worldObject.Guid))
            {
                Session.Network.EnqueueSend(new GameEventInventoryRemoveObject(Session, worldObject));

                if (!isFromMergeEvent)
                {
                    Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

                    if (worldObject.WeenieType == WeenieType.Coin)
                        UpdateCoinValue();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes an item from either the player's inventory, or equipped items, and sends network messages
        /// </summary>
        public bool TryRemoveItemWithNetworking(WorldObject item)
        {
            if (item.CurrentWieldedLocation != null)
            {
                if (!UnwieldItemWithNetworking(this, item))
                {
                    log.Warn($"Player_Inventory.TryRemoveItemWithNetworking: couldn't unwield item from {Name} ({item.Name})");
                    return false;
                }
            }

            return TryRemoveFromInventoryWithNetworking(item);
        }


        // =====================================
        // Helper Functions - Inventory Movement
        // =====================================

        [Flags]
        private enum SearchLocations
        {
            None                = 0x00,
            MyInventory         = 0x01,
            MyEquippedItems     = 0x02,
            Landblock           = 0x04,
            LastUsedContainer   = 0x08,
            Everywhere          = 0xFF
        }

        private WorldObject FindObject(ObjectGuid objectGuid, SearchLocations searchLocations, out Container foundInContainer, out Container rootContainer)
        {
            WorldObject result;

            foundInContainer = null;
            rootContainer = null;

            if (searchLocations.HasFlag(SearchLocations.MyInventory))
            {
                result = GetInventoryItem(objectGuid, out foundInContainer);

                if (result != null)
                {
                    rootContainer = this;
                    return result;
                }
            }

            if (searchLocations.HasFlag(SearchLocations.MyEquippedItems))
            {
                result = GetEquippedItem(objectGuid);

                if (result != null)
                {
                    rootContainer = this;
                    return result;
                }
            }

            if (searchLocations.HasFlag(SearchLocations.Landblock))
            {
                result = CurrentLandblock?.GetObject(objectGuid);

                if (result != null)
                    return result;
            }

            if (searchLocations.HasFlag(SearchLocations.LastUsedContainer))
            {
                var lastUsedContainer = CurrentLandblock?.GetObject(lastUsedContainerId) as Container;

                if (lastUsedContainer != null)
                {
                    result = lastUsedContainer.GetInventoryItem(objectGuid, out foundInContainer);

                    if (result != null)
                    {
                        rootContainer = lastUsedContainer;

                        return result;
                    }
                }
            }

            return null;
        }

        // TODO: deprecate this
        // it is not the responsibility of Player_Use to convert ObjectGuids into WorldObjects
        // this should be done much earlier, at the beginning of the HandleAction methods
        private ActionChain CreateMoveToChain(ObjectGuid targetGuid, out int thisMoveToChainNumber)
        {
            var item = FindObject(targetGuid, SearchLocations.Landblock | SearchLocations.LastUsedContainer, out var foundInContainer, out var rootContainer);

            if (item == null && rootContainer == null)
            {
                thisMoveToChainNumber = moveToChainCounter;
                return null;
            }

            if (rootContainer != null)
                return CreateMoveToChain(rootContainer.Guid, out thisMoveToChainNumber);

            return CreateMoveToChain(item.Guid, out thisMoveToChainNumber);
        }

        /// <summary>
        /// This method is used to pick items off the world - out of 3D space and into our inventory or to a wielded slot.
        /// It checks the use case needed, sends the appropriate response messages.
        /// In addition, it will move to objects that are out of range in the attempt to pick them up.
        /// It will call update appearance if needed and you have wielded an item from the ground. Og II
        /// </summary>
        private void PickupItemWithNetworking(Container container, ObjectGuid itemGuid, int placementPosition, PropertyInstanceId iidPropertyId)
        {
            //var item = GetPickupItem(itemGuid, out bool itemWasRestingOnLandblock);
            var item = FindObject(itemGuid, SearchLocations.Landblock | SearchLocations.LastUsedContainer, out var foundInContainer, out var rootContainer);
            if (item == null) return;
            var itemWasRestingOnLandblock = (rootContainer == null);

            var targetLocation = rootContainer ?? item;

            // rotate / move towards object
            // TODO: only do this if not within use distance
            ActionChain pickUpItemChain = new ActionChain();
            pickUpItemChain.AddChain(CreateMoveToChain(targetLocation, out var thisMoveToChainNumber));

            var thisMoveToChainNumberCopy = thisMoveToChainNumber;

            // rotate towards object
            // TODO: should rotating be added directly to moveto chain?

            /*pickUpItemChain.AddAction(this, () => Rotate(targetLocation));
            var angle = GetAngle(targetLocation);
            var rotateTime = GetRotateDelay(angle);
            pickUpItemChain.AddDelaySeconds(rotateTime);*/

            pickUpItemChain.AddAction(this, () =>
            {
                /*if (thisMoveToChainNumberCopy != moveToChainCounter)
                {
                    // todo we need to break the pickUpItemChain to stop further elements from executing
                    // todo alternatively, we create a MoveToManager (see the physics implementation) to manage this.
                    // todo I figured having the ability to someChain.BreakChain() might come in handy in the future. - Mag
                    return;
                }*/

                // start picking up item animation
                var motion = new Motion(CurrentMotionState.Stance, MotionCommand.Pickup);
                EnqueueBroadcast(new GameMessageUpdatePosition(this), new GameMessageUpdateMotion(this, motion));
            });

            // Wait for animation to progress
            var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId);
            var pickupAnimationLength = motionTable.GetAnimationLength(CurrentMotionState.Stance, MotionCommand.Pickup, MotionCommand.Ready);
            pickUpItemChain.AddDelaySeconds(pickupAnimationLength);

            // pick up item
            pickUpItemChain.AddAction(this, () =>
            {
                // handle quest items
                var questSolve = false;

                if (item.Quest != null)
                {
                    if (!QuestManager.CanSolve(item.Quest))
                    {
                        QuestManager.HandleSolveError(item.Quest);
                        return;
                    }
                    else
                        questSolve = true;
                }

                if (itemWasRestingOnLandblock)
                {
                    if (CurrentLandblock != null && CurrentLandblock.RemoveWorldObjectFromPickup(itemGuid))
                        item.NotifyOfEvent(RegenerationType.PickUp);
                }
                else
                {
                    var lastUsedContainer = CurrentLandblock?.GetObject(lastUsedContainerId) as Container;
                    if (lastUsedContainer.TryRemoveFromInventory(itemGuid, out item))
                    {
                        item.NotifyOfEvent(RegenerationType.PickUp);
                    }
                    else
                    {
                        // Item is in the container which we should have open
                        log.Error($"{Name}.PickUpItemWithNetworking({itemGuid}): picking up items from world containers side pack WIP");
                        return;
                    }

                    var containerInventory = lastUsedContainer.Inventory;

                    var lastUsedHook = lastUsedContainer as Hook;
                    if (lastUsedHook != null)
                        lastUsedHook.OnRemoveItem();
                }

                // If the item still has a location, CurrentLandblock failed to remove it
                if (item.Location != null)
                {
                    Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.NoObject));
                    log.Error("Player_Inventory PickupItemWithNetworking item.Location != null");
                    return;
                }

                // If the item has a ContainerId, it was probably picked up by someone else before us
                if (itemWasRestingOnLandblock && item.ContainerId != null && item.ContainerId != 0)
                {
                    log.Error("Player_Inventory PickupItemWithNetworking item.ContainerId != 0");
                    Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.ObjectGone));
                    return;
                }

                item.SetPropertiesForContainer();

                // FIXME(ddevec): I'm not 100% sure which of these need to be broadcasts, and which are local sends...

                if (iidPropertyId == PropertyInstanceId.Container)
                {
                    if (!container.TryAddToInventory(item, placementPosition, true))
                    {
                        Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.NoObject));
                        log.Error("Player_Inventory PickupItemWithNetworking TryAddToInventory failed");
                        return;
                    }

                    // If we've put the item to a side pack, we must increment our main EncumbranceValue and Value
                    if (container != this && container.ContainerId == Guid.Full)
                    {
                        EncumbranceVal += item.EncumbranceVal;
                        Value += item.Value;
                    }

                    if (item is Container itemAsContainer)
                    {
                        Session.Network.EnqueueSend(new GameEventViewContents(Session, itemAsContainer));

                        foreach (var packItem in itemAsContainer.Inventory)
                            Session.Network.EnqueueSend(new GameMessageCreateObject(packItem.Value));
                    }

                    Session.Network.EnqueueSend(
                        new GameMessageSound(Guid, Sound.PickUpItem, 1.0f),
                        new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, container.Guid),
                        new GameEventItemServerSaysContainId(Session, item, container));
                }
                else if (iidPropertyId == PropertyInstanceId.Wielder)
                {
                    // wield requirements check
                    var canWield = CheckWieldRequirement(item);
                    if (canWield != WeenieError.None)
                    {
                        Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, canWield));
                        return;
                    }
                    if (!TryEquipObject(item, placementPosition))
                    {
                        Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.NoObject));
                        return;
                    }
                }

                EnqueueBroadcast(new GameMessagePickupEvent(item));

                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

                if (item.WeenieType == WeenieType.Coin)
                    UpdateCoinValue();

                if (iidPropertyId == PropertyInstanceId.Wielder)
                    TryWieldItem(item, placementPosition);

                if (questSolve)
                    QuestManager.Update(item.Quest);
            });

            // return to previous stance
            pickUpItemChain.AddAction(this, () =>
            {
                var motion = new Motion(CurrentMotionState.Stance);
                EnqueueBroadcastMotion(motion);
            });

            // Set chain to run
            pickUpItemChain.EnqueueChain();
        }

        /// <summary>
        /// This method is called in response to a put item in container message. It is used when the item going into a container was wielded.
        /// It sets the appropriate properties, sends out response messages  and handles switching stances - for example if you have a bow wielded and are in bow combat stance,
        /// when you unwield the bow, this also sends the messages needed to go into unarmed combat mode. Og II
        /// </summary>
        private bool UnwieldItemWithNetworking(Container container, WorldObject item, int placement = 0)
        {
            EquipMask? oldLocation = item.CurrentWieldedLocation;

            // If item has any spells, remove them from the registry on unequip
            if (item.Biota.BiotaPropertiesSpellBook != null)
            {
                for (int i = 0; i < item.Biota.BiotaPropertiesSpellBook.Count; i++)
                    DispelItemSpell(item.Guid, (uint)item.Biota.BiotaPropertiesSpellBook.ElementAt(i).Spell);
            }

            if (!TryDequipObject(item.Guid))
            {
                log.Error("Player_Inventory UnwieldItemWithNetworking TryDequipObject failed");
                return false;
            }

            item.SetPropertiesForContainer();

            if (!container.TryAddToInventory(item, placement))
            {
                log.Error("Player_Inventory UnwieldItemWithNetworking TryAddToInventory failed");
                return false;
            }

            // If we've unwielded the item to a side pack, we must increment our main EncumbranceValue and Value
            if (container != this && container.ContainerId == Guid.Full)
            {
                EncumbranceVal += item.EncumbranceVal;
                Value += item.Value;
            }
            // todo I think we need to recalc our SetupModel here. see CalculateObjDesc()

            EnqueueBroadcast(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Wielder, new ObjectGuid(0)),
                new GameMessagePublicUpdatePropertyInt(item, PropertyInt.CurrentWieldedLocation, 0),
                new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, container.Guid),
                new GameMessagePickupEvent(item),
                new GameMessageSound(Guid, Sound.UnwieldObject, (float)1.0),
                new GameEventItemServerSaysContainId(Session, item, container),
                new GameMessageObjDescEvent(this));

            if (CombatMode == CombatMode.NonCombat || (oldLocation != EquipMask.MeleeWeapon && oldLocation != EquipMask.MissileWeapon && oldLocation != EquipMask.Held && oldLocation != EquipMask.Shield))
                return true;

            SetCombatMode(CombatMode.Melee);
            return true;
        }

        /// <summary>
        /// Method is called in response to put item in container message. This use case is we are just reorganizing our items.
        /// It is either a in pack slot to slot move, or we could be going from one pack (container) to another.
        /// This method is called from an action chain.  Og II
        /// </summary>
        /// <param name="item">the item we are moving</param>
        /// <param name="container">what container are we going in</param>
        /// <param name="placementPosition">what is my slot position within that container</param>
        private void MoveItemWithNetworking(WorldObject item, Container container, int placementPosition)
        {
            if (!TryRemoveFromInventory(item.Guid))
            {
                log.Error("Player_Inventory MoveItemWithNetworking TryRemoveFromInventory failed");
                return;
            }

            if (!container.TryAddToInventory(item, placementPosition))
            {
                log.Error("Player_Inventory MoveItemWithNetworking TryAddToInventory failed");
                return;
            }

            // If we've moved the item to a side pack, we must increment our main EncumbranceValue and Value
            if (container != this && container.ContainerId == Guid.Full)
            {
                EncumbranceVal += item.EncumbranceVal;
                Value += item.Value;
            }

            Session.Network.EnqueueSend(
                new GameEventItemServerSaysContainId(Session, item, container),
                new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, container.Guid));
        }

        // =========================================
        // Game Action Handlers - Inventory Movement 
        // =========================================
        // These are raised by client actions

        /// <summary>
        /// This is raised when we:
        /// - move an item around in our inventory.
        /// - dequip an item.
        /// - Pickup an item off of the landblock or a container on the lanblock
        /// </summary>
        public void HandleActionPutItemInContainer(ObjectGuid itemGuid, ObjectGuid containerGuid, int placement = 0)
        {
            bool containerOwnedByPlayer = true;

            Container container;
            
            if (containerGuid == Guid)
                container = this; // Destination is main pack
            else
                container = (Container)GetInventoryItem(containerGuid); // Destination is side pack

            if (container == null) // Destination is a container in the world, not in our possession
            {
                containerOwnedByPlayer = false;
                container = CurrentLandblock?.GetObject(containerGuid) as Container;

                if (container == null) // Container is a container within a container in the world....
                {
                    var lastUsedContainer = CurrentLandblock?.GetObject(lastUsedContainerId) as Container;

                    if (lastUsedContainer != null && lastUsedContainer.Inventory.TryGetValue(containerGuid, out var value))
                        container = value as Container;
                }
            }

            if (container == null)
            {
                log.Error("Player_Inventory HandleActionPutItemInContainer container not found");
                return;
            }

            var item = GetInventoryItem(itemGuid) ?? GetEquippedItem(itemGuid);
                        
            if (item != null)
            {
                //Console.WriteLine($"HandleActionPutItemInContainer({item.Name})");

                if ((item.Attuned ?? 0) == 1 && containerOwnedByPlayer == false)
                {
                    Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.AttunedItem));
                    Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, this));
                    return;
                }
            }

            var corpse = container as Corpse;
            if (corpse != null)
            {
                if (corpse.IsMonster == false)
                {
                    Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.Dead));
                    return;
                }
            }

            // Is this something I already have? If not, it has to be a pickup - do the pickup and out.
            if (item == null)
            {
                var itemToPickup = CurrentLandblock?.GetObject(itemGuid);

                if (itemToPickup != null)
                {
                    //Checking to see if item to pick is an container itself
                    if (itemToPickup.WeenieType == WeenieType.Container)
                    {
                        //Check to see if the container is open
                        if (itemToPickup.IsOpen)
                        {
                            var containerToPickup = CurrentLandblock?.GetObject(itemGuid) as Container;

                            if (containerToPickup.Viewer == Session.Player.Guid.Full)
                            {
                                //We're the one that has it open. Close it before picking it up
                                containerToPickup.Close(Session.Player);
                            }
                            else
                            {
                                //We're not who has it open. Can't pick up something someone else is viewing!

                                //TODO: These messages are what I remember of retail. I was unable to confirm or deny with PCAPs
                                //TODO: This will likley need revisited at some point to align with retail
                                Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(Session, WeenieErrorWithString.The_IsCurrentlyInUse, itemToPickup.Name));
                                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.Stuck));
                                return;
                            }
                        }
                    }
                }

                // This is a pickup into our main pack.
                PickupItemWithNetworking(container, itemGuid, placement, PropertyInstanceId.Container);
                return;
            }

            // Ok, I know my container and I know I must have the item so let's get it.

            // Was I equiped? If so, lets take care of that and unequip
            if (item.WielderId != null)
            {
                UnwieldItemWithNetworking(container, item, placement);
                item.IsAffecting = false;
                return;
            }

            // if were are still here, this needs to do a pack pack or main pack move.
            MoveItemWithNetworking(item, container, placement);

            container.OnAddItem();
        }

        /// <summary>
        /// This is raised when we drop an item. It can be a wielded item, or an item in our inventory.
        /// </summary>
        public void HandleActionDropItem(ObjectGuid itemGuid)
        {
            // check packs of item.
            WorldObject item = FindObject(itemGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out _, out _);

            if (item == null)
            {
                log.Error("Player_Inventory HandleActionDropItem item is null");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.YouDoNotOwnThatItem));
                return;
            }

            if ((item.Attuned ?? 0) == 1 || (item.Bonded ?? 0) == 1)
            {
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.AttunedItem));
                return;
            }

            if (!TryRemoveFromInventory(itemGuid, out item))
            {
                // check to see if this item is wielded
                if (TryDequipObject(itemGuid, out item))
                {
                    EnqueueBroadcast(new GameMessageSound(Guid, Sound.WieldObject, 1.0f),
                        new GameMessageObjDescEvent(this),
                        new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Wielder, new ObjectGuid(0)));
                }
            }

            //var motion = new Motion(MotionStance.NonCombat);
            var motion = new Motion(this, MotionCommand.Pickup);
            Session.Network.EnqueueSend(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, new ObjectGuid(0)));

            // Set drop motion
            EnqueueBroadcastMotion(motion);
            
            // Now wait for Drop Motion to finish -- use ActionChain
            var dropChain = new ActionChain();

            // Wait for drop animation
            var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId);
            var pickupAnimationLength = motionTable.GetAnimationLength(CurrentMotionState.Stance, MotionCommand.Pickup, MotionCommand.Ready);
            dropChain.AddDelaySeconds(pickupAnimationLength);

            // Play drop sound
            // Put item on landblock
            dropChain.AddAction(this, () =>
            {
                if (CurrentLandblock == null)
                    return; // Maybe we were teleported as we were motioning to drop the item

                var returnStance = new Motion(CurrentMotionState.Stance);
                EnqueueBroadcastMotion(returnStance);

                EnqueueBroadcast(new GameMessageSound(Guid, Sound.DropItem, (float)1.0));

                Session.Network.EnqueueSend(
                    new GameEventItemServerSaysMoveItem(Session, item),
                    new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, new ObjectGuid(0)),
                    new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

                if (item.WeenieType == WeenieType.Coin)
                    UpdateCoinValue();

                // This is the sequence magic - adds back into 3d space seem to be treated like teleport.
                item.Sequences.GetNextSequence(SequenceType.ObjectTeleport);
                item.Sequences.GetNextSequence(SequenceType.ObjectVector);

                item.SetPropertiesForWorld(this, 1.1f);

                CurrentLandblock?.AddWorldObject(item);

                EnqueueBroadcast(new GameMessageUpdatePosition(item));

                // We must update the database with the latest ContainerId and WielderId properties.
                // If we don't, the player can drop the item, log out, and log back in. If the landblock hasn't queued a database save in that time,
                // the player will end up loading with this object in their inventory even though the landblock is the true owner. This is because
                // when we load player inventory, the database still has the record that shows this player as the ContainerId for the item.
                item.SaveBiotaToDatabase();
            });

            dropChain.EnqueueChain();
        }

        /// <summary>
        /// create spells by an equipped item
        /// </summary>
        /// <param name="item">the equipped item doing the spell creation</param>
        /// <param name="suppressSpellChatText">prevent spell text from being sent to the player's chat windows</param>
        /// <param name="ignoreRequirements">disregard item activation requirements</param>
        /// <returns>if any spells were created or not</returns>
        public bool CreateEquippedItemSpells(WorldObject item, bool suppressSpellChatText = false, bool ignoreRequirements = false)
        {
            bool spellCreated = false;
            if (item.Biota.BiotaPropertiesSpellBook != null)
            {
                // TODO: Once Item Current Mana is fixed for loot generated items, '|| item.ItemCurMana == null' can be removed
                if (item.ItemCurMana > 1 || item.ItemCurMana == null)
                {
                    for (int i = 0; i < item.Biota.BiotaPropertiesSpellBook.Count; i++)
                    {
                        if (CreateItemSpell(item.Guid, (uint)item.Biota.BiotaPropertiesSpellBook.ElementAt(i).Spell, suppressSpellChatText, ignoreRequirements))
                            spellCreated = true;
                    }
                    item.IsAffecting = spellCreated;
                    if (item.IsAffecting ?? false)
                    {
                        if (item.ItemCurMana.HasValue)
                            item.ItemCurMana--;
                    }
                }
            }
            return spellCreated;
        }

        /// <summary>
        /// Called when network message is received for 'GetAndWieldItem'
        /// </summary>
        public void HandleActionGetAndWieldItem(uint itemId, int wieldLocation)
        {
            var itemGuid = new ObjectGuid(itemId);

            // handle inventory item -> weapon/shield slot
            var item = GetInventoryItem(itemGuid);
            if (item != null)
            {
                //Console.WriteLine($"HandleActionGetAndWieldItem({item.Name})");

                var result = TryWieldItem(item, wieldLocation);
                return;
            }

            // handle 1 wielded slot -> the other wielded slot
            // (weapon swap)
            var wieldedItem = GetEquippedItem(itemGuid);
            if (wieldedItem != null)
            {
                var result = TryWieldItem(wieldedItem, wieldLocation);
                return;
            }

            // We don't have possession of the item so we must pick it up.
            // should this be wielding the item afterwards?
            PickupItemWithNetworking(this, itemGuid, wieldLocation, PropertyInstanceId.Wielder);
        }

        public bool TryWieldItem(WorldObject item, int wieldLocation, bool preCheck = false)
        {
            //Console.WriteLine($"TryWieldItem({item.Name}, {(EquipMask)wieldLocation})");

            var wieldError = CheckWieldRequirement(item);

            if (wieldError != WeenieError.None)
            {
                var containerId = (uint)item.ContainerId;
                var container = GetInventoryItem(new ObjectGuid(containerId));
                if (container == null) container = this;

                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, errorType: wieldError));
                Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, container));
                return false;
            }

            // unwield wand / missile launcher if dual wielding
            if ((EquipMask)wieldLocation == EquipMask.Shield && !item.IsShield)
            {
                var mainWeapon = EquippedObjects.Values.FirstOrDefault(e => e.CurrentWieldedLocation == EquipMask.MissileWeapon || e.CurrentWieldedLocation == EquipMask.Held);
                if (mainWeapon != null)
                {
                    if (!UnwieldItemWithNetworking(this, mainWeapon))
                        return false;
                }
            }

            TryRemoveFromInventory(item.Guid, out var containerItem);

            if (!TryEquipObject(item, wieldLocation))
            {
                log.Error("Player_Inventory HandleActionGetAndWieldItem TryEquipObject failed");
                return false;
            }

            CreateEquippedItemSpells(item);

            // TODO: I think we need to recalc our SetupModel here. see CalculateObjDesc()
            var msgWieldItem = new GameEventWieldItem(Session, item.Guid.Full, wieldLocation);
            var sound = new GameMessageSound(Guid, Sound.WieldObject, 1.0f);

            if ((EquipMask)wieldLocation != EquipMask.MissileAmmo)
            {
                var updateContainer = new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, new ObjectGuid(0));
                var updateWielder = new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Wielder, Guid);
                var updateWieldLoc = new GameMessagePublicUpdatePropertyInt(item, PropertyInt.CurrentWieldedLocation, wieldLocation);

                if (((EquipMask)wieldLocation & EquipMask.Selectable) == 0)
                {
                    EnqueueBroadcast(msgWieldItem, sound, updateContainer, updateWielder, updateWieldLoc, new GameMessageObjDescEvent(this));
                    return true;
                }

                // TODO: wait for HandleQueueStance() here?
                EnqueueBroadcast(new GameMessageParentEvent(this, item, (int?)item.ParentLocation ?? 0, (int?)item.Placement ?? 0), msgWieldItem, sound, updateContainer, updateWielder, updateWieldLoc);

                if (CombatMode == CombatMode.NonCombat || CombatMode == CombatMode.Undef)
                    return true;

                switch ((EquipMask)wieldLocation)
                {
                    case EquipMask.MissileWeapon:
                        SetCombatMode(CombatMode.Missile);
                        break;
                    case EquipMask.Held:
                        SetCombatMode(CombatMode.Magic);
                        break;
                    default:
                        SetCombatMode(CombatMode.Melee);
                        break;
                }
            }
            else
            {
                Session.Network.EnqueueSend(msgWieldItem, sound);

                // new ammo becomes visible
                // FIXME: can't get this to work without breaking client
                // existing functionality also broken while swapping multiple arrows in missile combat mode
                /*if (CombatMode == CombatMode.Missile)
                {
                    EnqueueBroadcast(new GameMessageParentEvent(this, item, (int)ACE.Entity.Enum.ParentLocation.RightHand, (int)ACE.Entity.Enum.Placement.RightHandCombat));
                }*/
            }

            return true;
        }

        public bool UseWieldRequirement = true;

        public WeenieError CheckWieldRequirement(WorldObject item)
        {
            if (!UseWieldRequirement) return WeenieError.None;

            var itemWieldReq = (WieldRequirement)(item.GetProperty(PropertyInt.WieldRequirements) ?? 0);
            switch (itemWieldReq)
            {
                case WieldRequirement.RawSkill:
                    // Check WieldDifficulty property against player's Skill level, defined by item's WieldSkilltype property
                    var itemSkillReq = ConvertToMoASkill((Skill)(item.GetProperty(PropertyInt.WieldSkilltype) ?? 0));

                    if (itemSkillReq != Skill.None)
                    {
                        var playerSkill = GetCreatureSkill(itemSkillReq).Current;

                        var skillDifficulty = (uint)(item.GetProperty(PropertyInt.WieldDifficulty) ?? 0);

                        if (playerSkill < skillDifficulty)
                            return WeenieError.SkillTooLow;
                    }
                    break;

                case WieldRequirement.Level:
                    // Check WieldDifficulty property against player's level
                    if (Level < (uint)(item.GetProperty(PropertyInt.WieldDifficulty) ?? 0))
                        return WeenieError.LevelTooLow;
                    break;

                case WieldRequirement.Attrib:
                    // Check WieldDifficulty property against player's Attribute, defined by item's WieldSkilltype property
                    var itemAttributeReq = (PropertyAttribute)(item.GetProperty(PropertyInt.WieldSkilltype) ?? 0);

                    if (itemAttributeReq != PropertyAttribute.Undef)
                    {
                        var playerAttribute = Attributes[itemAttributeReq].Current;

                        if (playerAttribute < (uint)(item.GetProperty(PropertyInt.WieldDifficulty) ?? 0))
                            return WeenieError.SkillTooLow;
                    }
                    break;
            }
            return WeenieError.None;
        }

        public Skill ConvertToMoASkill(Skill skill)
        {
            var player = this as Player;
            if (player != null)
            {
                if (SkillExtensions.RetiredMelee.Contains(skill))
                    return player.GetHighestMeleeSkill();
                if (SkillExtensions.RetiredMissile.Contains(skill))
                    return Skill.MissileWeapons;
            }
            return skill;
        }

        /// <summary>
        /// Called when player attempts to give an object to someone else,
        /// ie. to another player, or NPC
        public void HandleActionGiveObjectRequest(ObjectGuid targetID, ObjectGuid itemGuid, uint amount)
        {
            var target = CurrentLandblock?.GetObject(targetID);
            var item = GetInventoryItem(itemGuid) ?? GetEquippedItem(itemGuid);
            if (target == null || item == null) return;

            // giver rotates to receiver
            var rotateDelay = Rotate(target);

            var giveChain = new ActionChain();
            giveChain.AddChain(CreateMoveToChain(targetID, out var thisMoveToChainNumber));

            if (target is Player)
                giveChain.AddAction(this, () => GiveObjecttoPlayer(target as Player, item, (ushort)amount));
            else
            {
                var receiveChain = new ActionChain();
                giveChain.AddAction(this, () =>
                {
                    GiveObjecttoNPC(target, item, amount, giveChain, receiveChain);
                    giveChain.AddChain(receiveChain);
                });
            }
            giveChain.EnqueueChain();
        }

        /// <summary>
        /// This code handle objects between players and other players
        /// </summary>
        private void GiveObjecttoPlayer(Player target, WorldObject item, ushort amount)
        {
            Player player = this;

            if ((item.Attuned ?? 0) == 1)
            {
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.AttunedItem));
                Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, this));
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.AttunedItem)); // Second message appears in PCAPs

                return;
            }

            if ((Character.CharacterOptions1 & (int)CharacterOptions1.LetOtherPlayersGiveYouItems) == (int)CharacterOptions1.LetOtherPlayersGiveYouItems)
            {
                if (target != player)
                {
                    // todo This should be refactored
                    // The order should be something like:
                    // See if target can accept the item
                    // Remove item from giver
                    // Save item to db
                    // Give item to receiver
                    if (target.HandlePlayerReceiveItem(item, player))
                    {
                        if (item.CurrentWieldedLocation != null)
                            UnwieldItemWithNetworking(this, item, 0);       // refactor, duplicate code from above

                        if (amount >= (item.StackSize ?? 1))
                        {
                            if (TryRemoveFromInventory(item.Guid, false))
                            {
                                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

                                if (item.WeenieType == WeenieType.Coin)
                                    UpdateCoinValue();
                            }
                        }
                        else
                        {
                            item.StackSize -= amount;

                            Session.Network.EnqueueSend(new GameMessageSetStackSize(item));

                            EncumbranceVal = (EncumbranceVal - (item.StackUnitEncumbrance * amount));
                            Value = (Value - (item.StackUnitValue * amount));

                            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

                            if (item.WeenieType == WeenieType.Coin)
                                UpdateCoinValue();
                        }

                        // We must update the database with the latest ContainerId and WielderId properties.
                        // If we don't, the player can give the item, log out, and log back in. If the receiver hasn't queued a database save in that time,
                        // the player will end up loading with this object in their inventory even though the receiver is the true owner. This is because
                        // when we load player inventory, the database still has the record that shows this player as the ContainerId for the item.
                        item.SaveBiotaToDatabase();

                        Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, target));
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"You give {target.Name} {item.Name}.", ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(new GameMessageSound(Guid, Sound.ReceiveItem, 1));
                    }
                }
            }
            else
            {
                Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(Session, WeenieErrorWithString._IsNotAcceptingGiftsRightNow, target.Name));
                Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, this));
            }
        }

        /// <summary>
        /// This code handles receiving objects from other players, ie. attempting to place the item in the target's inventory
        /// </summary>
        private bool HandlePlayerReceiveItem(WorldObject item, Player player)
        {
            if ((this as Player) == null)
                return false;

            Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} gives you {item.Name}.", ChatMessageType.Broadcast));
            Session.Network.EnqueueSend(new GameMessageSound(Guid, Sound.ReceiveItem, 1));

            TryCreateInInventoryWithNetworking(item);

            return true;
        }

        /// <summary>
        /// This code handles objects between players and other world objects
        /// </summary>
        private void GiveObjecttoNPC(WorldObject target, WorldObject item, uint amount, ActionChain giveChain, ActionChain receiveChain)
        {
            if (target == null || item == null) return;

            if (target.EmoteManager.IsBusy)
            {
                giveChain.AddAction(this, () =>
                {
                    Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(Session, WeenieErrorWithString._IsTooBusyToAcceptGifts, target.Name));
                    Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, this));
                });
            }
            else if (target.GetProperty(PropertyBool.AiAcceptEverything) ?? false)
            {
                // NPC accepts any item
                giveChain.AddAction(this, () => ItemAccepted(item, amount, target));
            }
            else if (!target.GetProperty(PropertyBool.AllowGive) ?? false)
            {
                giveChain.AddAction(this, () =>
                {
                    Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(Session, WeenieErrorWithString._IsNotAcceptingGiftsRightNow, target.Name));
                    Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, this));
                });
            }
            else if (((item.GetProperty(PropertyInt.Attuned) ?? 0) == 1) && ((target as Player) != null))
            {
                giveChain.AddAction(this, () =>
                {
                    Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.AttunedItem));
                    Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, this));
                    Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.AttunedItem)); // Second message appears in PCAPs
                });
            }
            else
            {
                var result = target.Biota.BiotaPropertiesEmote.Where(emote => emote.WeenieClassId == item.WeenieClassId);
                WorldObject player = this;
                if (target.HandleNPCReceiveItem(item, player, receiveChain))
                {
                    if (result.ElementAt(0).Category == (uint)EmoteCategory.Give)
                    {
                        // Item accepted by collector/NPC
                        giveChain.AddAction(this, () => ItemAccepted(item, amount, target));
                    }
                    else if (result.ElementAt(0).Category == (uint)EmoteCategory.Refuse)
                    {
                        // Item rejected by npc
                        giveChain.AddAction(this, () =>
                        {
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"You allow {target.Name} to examine your {item.Name}.", ChatMessageType.Broadcast));
                            Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.TradeAiRefuseEmote));
                        });
                    }
                }
                else
                {
                    giveChain.AddAction(this, () =>
                    {
                        Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.TradeAiDoesntWant));
                        Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(Session, (WeenieErrorWithString)WeenieError.TradeAiDoesntWant, target.Name));
                    });
                }
            }
        }

        /// <summary>
        /// Giver methods used upon successful acceptance of item by NPC<para />
        /// The item will be destroyed after processing.
        /// </summary>
        private void ItemAccepted(WorldObject item, uint amount, WorldObject target)
        {
            if (item.CurrentWieldedLocation != null)
                UnwieldItemWithNetworking(this, item, 0);       // refactor, duplicate code from above

            TryRemoveItemFromInventoryWithNetworkingWithDestroy(item, (ushort)amount);

            Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, item, target));
            Session.Network.EnqueueSend(new GameMessageSystemChat($"You give {target.Name} {item.Name}.", ChatMessageType.Broadcast));
            Session.Network.EnqueueSend(new GameMessageSound(Guid, Sound.ReceiveItem, 1));

            Session.Network.EnqueueSend(new GameEventInventoryRemoveObject(Session, item));

            item.Destroy();
        }


        // =====================================
        // Helper Functions - Inventory Stacking
        // =====================================
        // Used by HandleActionStackableSplitToContainer

        /// <summary>
        /// This method handles the second part of the merge if we have not merged ALL of the fromWo into the toWo - split out for code reuse.
        /// It calculates the updated values for stack size, value and burden, creates the needed client messages and sends them.
        /// This must be called from within an action chain. Og II
        /// </summary>
        /// <param name="fromWo">World object of the item are we merging from</param>
        /// <param name="amount">How many are we merging fromWo into the toWo</param>
        private void UpdateFromStack(WorldObject fromWo, int amount)
        {
            Debug.Assert(fromWo.Value != null, "fromWo.Value != null");
            Debug.Assert(fromWo.StackSize != null, "fromWo.StackSize != null");
            Debug.Assert(fromWo.EncumbranceVal != null, "fromWo.EncumbranceVal != null");

            // ok, there are some left, we need up update the stack size, value and burden of the fromWo
            int newFromValue = (int)(fromWo.Value + ((fromWo.Value / fromWo.StackSize) * -amount));
            uint newFromBurden = (uint)(fromWo.EncumbranceVal + ((fromWo.EncumbranceVal / fromWo.StackSize) * -amount));

            int oldFromStackSize = (int)fromWo.StackSize;
            fromWo.StackSize -= (ushort)amount;
            fromWo.Value = newFromValue;
            fromWo.EncumbranceVal = (int)newFromBurden;

            // Build the needed messages to the client.
            EnqueueBroadcast(new GameMessageSetStackSize(fromWo));
        }

        /// <summary>
        /// This method handles the first part of the merge - split out for code reuse.
        /// It calculates the updated values for stack size, value and burden, creates the needed client messages and sends them.
        /// This must be called from within an action chain. Og II
        /// </summary>
        /// <param name="fromWo">World object of the item are we merging from</param>
        /// <param name="toWo">World object of the item we are merging into</param>
        /// <param name="amount">How many are we merging fromWo into the toWo</param>
        private void UpdateToStack(WorldObject fromWo, WorldObject toWo, int amount, bool missileAmmo = false)
        {
            Debug.Assert(toWo.Value != null, "toWo.Value != null");
            Debug.Assert(fromWo.Value != null, "fromWo.Value != null");
            Debug.Assert(toWo.StackSize != null, "toWo.StackSize != null");
            Debug.Assert(fromWo.StackSize != null, "fromWo.StackSize != null");
            Debug.Assert(toWo.EncumbranceVal != null, "toWo.EncumbranceVal != null");
            Debug.Assert(fromWo.EncumbranceVal != null, "fromWo.EncumbranceVal != null");

            int newValue = (int)(toWo.Value + ((fromWo.Value / fromWo.StackSize) * amount));
            uint newBurden = (uint)(toWo.EncumbranceVal + ((fromWo.EncumbranceVal / fromWo.StackSize) * amount));

            int oldStackSize = (int)toWo.StackSize;
            toWo.StackSize += (ushort)amount;
            toWo.Value = newValue;
            toWo.EncumbranceVal = (int)newBurden;

            // Build the needed messages to the client.
            if (missileAmmo)
                EnqueueBroadcast(new GameMessageSetStackSize(toWo));
            else
                EnqueueBroadcast(new GameEventItemServerSaysContainId(Session, toWo, this), new GameMessageSetStackSize(toWo));
        }


        // =========================================
        // Game Action Handlers - Inventory Stacking 
        // =========================================
        // These are raised by client actions

        /// <summary>
        /// This method is used to split a stack of any item that is stackable - arrows, tapers, pyreal etc.
        /// It creates the new object and sets the burden of the new item, adjusts the count and burden of the splitting item. Og II
        /// </summary>
        /// <param name="stackId">This is the guild of the item we are splitting</param>
        /// <param name="containerId">The guid of the container</param>
        /// <param name="placementPosition">Place is the slot in the container we are splitting into. Range 0-MaxCapacity</param>
        /// <param name="amount">The amount of the stack we are splitting from that we are moving to a new stack.</param>
        public void HandleActionStackableSplitToContainer(uint stackId, uint containerId, int placementPosition, ushort amount)
        {
            Container sourceContainer;
            bool sourceContainerIsOnLandblock = false;

            Container targetContainer;
            bool targetContainerIsOnLandblock = false;

            // Init our source vars
            var stack = GetInventoryItem(new ObjectGuid(stackId), out sourceContainer);

            if (stack == null)
            {
                stack = CurrentLandblock?.GetObject(new ObjectGuid(stackId));

                if (stack == null)
                {
                    sourceContainer = CurrentLandblock?.GetObject(lastUsedContainerId) as Container;

                    if (sourceContainer != null)
                    {
                        stack = sourceContainer.GetInventoryItem(new ObjectGuid(stackId), out sourceContainer);

                        if (stack != null)
                            sourceContainerIsOnLandblock = true;
                    }
                }
            }

            if (stack == null)
            {
                log.Error("Player_Inventory HandleActionStackableSplitToContainer stack not found");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.YouDoNotOwnThatItem));
                return;
            }

            if (stack.Value == null || stack.StackSize < amount || stack.StackSize == 0)
            {
                log.Error("Player_Inventory HandleActionStackableSplitToContainer stack not large enough for amount");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.BadParam));
                return;
            }

            // Init our target vars
            if (containerId == Guid.Full)
                targetContainer = this;
            else
                targetContainer = GetInventoryItem(new ObjectGuid(containerId)) as Container;

            if (targetContainer == null)
            {
                targetContainer = CurrentLandblock?.GetObject(containerId) as Container;

                if (targetContainer == null)
                {
                    var lastUsedContainer = CurrentLandblock?.GetObject(lastUsedContainerId) as Container;

                    if (lastUsedContainer != null)
                        targetContainer = lastUsedContainer.GetInventoryItem(new ObjectGuid(containerId)) as Container;
                }

                targetContainerIsOnLandblock = (targetContainer != null);
            }

            if (targetContainer == null)
            {
                log.Error("Player_Inventory HandleActionStackableSplitToContainer container not found");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.YouDoNotOwnThatItem));
                return;
            }

            // Ok we are in business

            if (sourceContainer == null || (sourceContainerIsOnLandblock && !targetContainerIsOnLandblock))
            {
                // Pickup from 3D
                // TODO
                return;
            }

            if (!sourceContainerIsOnLandblock && targetContainerIsOnLandblock)
            {
                // Drop to 3D
                // TODO
                return;
            }

            // TODO we need animation of we're going to/from 3D

            var newStack = WorldObjectFactory.CreateNewWorldObject(stack.WeenieClassId);
            newStack.StackSize = amount;
            newStack.EncumbranceVal = (newStack.StackUnitEncumbrance ?? 0) * (newStack.StackSize ?? 1);
            newStack.Value = (newStack.StackUnitValue ?? 0) * (newStack.StackSize ?? 1);

            // Before we modify the original stack, we make sure we can add the new stack
            if (!targetContainer.TryAddToInventory(newStack, placementPosition, true))
            {
                log.Error("Player_Inventory HandleActionStackableSplitToContainer TryAddToInventory failed");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.BadParam));
                return;
            }

            stack.StackSize -= amount;
            stack.EncumbranceVal = (stack.StackUnitEncumbrance ?? 0) * (stack.StackSize ?? 1);
            stack.Value = (stack.StackUnitValue ?? 0) * (stack.StackSize ?? 1);

            Session.Network.EnqueueSend(new GameMessageSetStackSize(stack));

            Session.Network.EnqueueSend(new GameMessageCreateObject(newStack));
            Session.Network.EnqueueSend(new GameEventItemServerSaysContainId(Session, newStack, targetContainer));

            // Adjust EncumbranceVal and Value

            sourceContainer.EncumbranceVal -= newStack.EncumbranceVal;
            sourceContainer.Value -= newStack.Value;
            
            if (sourceContainer == this && sourceContainer != targetContainer && !targetContainerIsOnLandblock)
            {
                // Add back the encunbrance and value back to the player since we moved it from the player to a side pack
                EncumbranceVal += newStack.EncumbranceVal;
                Value += newStack.Value;
            }

            if ((sourceContainerIsOnLandblock || targetContainerIsOnLandblock) && sourceContainerIsOnLandblock != targetContainerIsOnLandblock)
            {
                // Between the player and an external pack

                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

                if (stack.WeenieType == WeenieType.Coin)
                    UpdateCoinValue();
            }
        }

        /// <summary>
        /// This method is used to split a stack of any item that is stackable - arrows, tapers, pyreal etc.
        /// It creates the new object and sets the burden of the new item, adjusts the count and burden of the splitting item.
        /// </summary>
        /// <param name="stackId">This is the guild of the item we are splitting</param>
        /// <param name="amount">The amount of the stack we are splitting from that we are moving to a new stack.</param>
        public void HandleActionStackableSplitTo3D(uint stackId, uint amount)
        {
            var stack = GetInventoryItem(new ObjectGuid(stackId), out var container);

            if (stack == null)
            {
                log.Error("Player_Inventory HandleActionStackableSplitToContainer stack not found");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.YouDoNotOwnThatItem));
                return;
            }

            if (stack.Value == null || stack.StackSize < amount || stack.StackSize == 0)
            {
                log.Error("Player_Inventory HandleActionStackableSplitToContainer stack not large enough for amount");
                Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(Session, WeenieError.BadParam));
                return;
            }

            // Ok we are in business

            var motion = new Motion(this, MotionCommand.Pickup);

            // Set drop motion
            EnqueueBroadcastMotion(motion);

            // Now wait for Drop Motion to finish -- use ActionChain
            var dropChain = new ActionChain();

            // Wait for drop animation
            var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId);
            var pickupAnimationLength = motionTable.GetAnimationLength(CurrentMotionState.Stance, MotionCommand.Pickup, MotionCommand.Ready);
            dropChain.AddDelaySeconds(pickupAnimationLength);

            // Play drop sound
            // Put item on landblock
            dropChain.AddAction(this, () =>
            {
                if (CurrentLandblock == null)
                    return; // Maybe we were teleported as we were motioning to drop the item

                stack.StackSize -= (ushort)amount;
                stack.EncumbranceVal = (stack.StackUnitEncumbrance ?? 0) * (stack.StackSize ?? 1);
                stack.Value = (stack.StackUnitValue ?? 0) * (stack.StackSize ?? 1);

                var newStack = WorldObjectFactory.CreateNewWorldObject(stack.WeenieClassId);
                newStack.StackSize = (ushort)amount;
                newStack.EncumbranceVal = (newStack.StackUnitEncumbrance ?? 0) * (newStack.StackSize ?? 1);
                newStack.Value = (newStack.StackUnitValue ?? 0) * (newStack.StackSize ?? 1);

                Session.Network.EnqueueSend(new GameMessageSetStackSize(stack));

                // Adjust EncumbranceVal and Value

                container.EncumbranceVal -= newStack.EncumbranceVal;
                container.Value -= newStack.Value;

                if (container != this)
                {
                    EncumbranceVal -= newStack.EncumbranceVal;
                    Value -= newStack.Value;
                }

                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.EncumbranceVal, EncumbranceVal ?? 0));

                if (stack.WeenieType == WeenieType.Coin)
                    UpdateCoinValue();

                var returnStance = new Motion(CurrentMotionState.Stance);
                EnqueueBroadcastMotion(returnStance);

                EnqueueBroadcast(new GameMessageSound(Guid, Sound.DropItem, 1.0f));

                // This is the sequence magic - adds back into 3d space seem to be treated like teleport.
                newStack.Sequences.GetNextSequence(SequenceType.ObjectTeleport);
                newStack.Sequences.GetNextSequence(SequenceType.ObjectVector);

                newStack.SetPropertiesForWorld(this, 1.1f);

                CurrentLandblock.AddWorldObject(newStack);
            });

            dropChain.EnqueueChain();
        }

        /// <summary>
        /// This method processes the Stackable Merge Game Action (F7B1) Stackable Merge (0x0054)
        /// </summary>
        /// <param name="mergeFromGuid">Guid of the item are we merging from</param>
        /// <param name="mergeToGuid">Guid of the item we are merging into</param>
        /// <param name="amount">How many are we merging fromGuid into the toGuid</param>
        public void HandleActionStackableMerge(ObjectGuid mergeFromGuid, ObjectGuid mergeToGuid, int amount)
        {
            // is this something I already have? If not, it has to be a pickup - do the pickup and out.
            if (!HasInventoryItem(mergeFromGuid))
            {
                // This is a pickup into our main pack.
                HandleActionPutItemInContainer(mergeFromGuid, Guid);
                return;
            }

            var fromItem = GetInventoryItem(mergeFromGuid);
            var toItem = GetInventoryItem(mergeToGuid);

            if (fromItem == null || toItem == null)
                return;

            // Check to see if we are trying to merge into a full stack. If so, nothing to do here.
            // Check this and see if I need to call UpdateToStack to clear the action with an amount of 0 Og II
            if (toItem.MaxStackSize == toItem.StackSize)
                return;

            var missileAmmo = toItem.ItemType == ItemType.MissileWeapon;

            if (toItem.MaxStackSize >= (ushort)((toItem.StackSize ?? 0) + amount))
            {
                // The toItem has enoguh capacity to take the full amount
                UpdateToStack(fromItem, toItem, amount, missileAmmo);

                // Ok did we merge it all? If so, let's remove the item.
                if (fromItem.StackSize == amount)
                    TryRemoveFromInventoryWithNetworking(fromItem, true);
                else
                    UpdateFromStack(fromItem, amount);
            }
            else
            {
                // The toItem does not have enough capacity to take the full amount. Just add what we can and adjust both.
                Debug.Assert(toItem.MaxStackSize != null, "toWo.MaxStackSize != null");

                var amtToFill = (toItem.MaxStackSize ?? 0) - (toItem.StackSize ?? 0);

                UpdateToStack(fromItem, toItem, amtToFill, missileAmmo);
                UpdateFromStack(toItem, amtToFill);
            }
        }


        // ===========================
        // Game Action Handlers - Misc
        // ===========================
        // These are raised by client actions

        /// <summary>
        /// This method handles inscription.   If you remove the inscription, it will remove the data from the object and
        /// remove it from the shard database - all inscriptions are stored in ace_object_properties_string Og II
        /// </summary>
        /// <param name="itemGuid">This is the object that we are trying to inscribe</param>
        /// <param name="inscriptionText">This is our inscription</param>
        public void HandleActionSetInscription(ObjectGuid itemGuid, string inscriptionText)
        {
            var item = GetInventoryItem(itemGuid) ?? GetEquippedItem(itemGuid);

            if (item == null)
            {
                log.Error("Player_Inventory HandleActionSetInscription failed");
                return;
            }

            if (item.Inscribable == true && item.ScribeName != "prewritten")
            {
                if (item.ScribeName != null && item.ScribeName != Name)
                {
                    ChatPacket.SendServerMessage(Session, "Only the original scribe may alter this without the use of an uninscription stone.", ChatMessageType.Broadcast);
                }
                else
                {
                    if (inscriptionText != "")
                    {
                        item.Inscription = inscriptionText;
                        item.ScribeName = Name;
                        item.ScribeAccount = Session.Account;
                        Session.Network.EnqueueSend(new GameEventInscriptionResponse(Session, item.Guid.Full, item.Inscription, item.ScribeName, item.ScribeAccount));
                    }
                    else
                    {
                        item.Inscription = null;
                        item.ScribeName = null;
                        item.ScribeAccount = null;
                    }
                }
            }
            else
            {
                // Send some cool you cannot inscribe that item message. Not sure how that was handled live, I could not find a pcap of a failed inscription. Og II
                ChatPacket.SendServerMessage(Session, "Target item cannot be inscribed.", ChatMessageType.System);
            }
        }
    }
}
