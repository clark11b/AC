using System;
using System.Collections.Concurrent;
using System.Linq;

using log4net;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// House hooks for item placement
    /// </summary>
    public class Hook : Container
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public House House { get => ParentLink as House; }

        public bool HasItem => Inventory != null && Inventory.Count > 0;

        public WorldObject Item => Inventory != null ? Inventory.Values.FirstOrDefault() : null;

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Hook(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Hook(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            IsLocked = false;
            IsOpen = false;

            // TODO: REMOVE ME?
            // Temporary workaround fix to account for ace spawn placement issues with certain hooked objects.
            var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);
            SetupTableId = weenie.GetProperty(PropertyDataId.Setup) ?? 0;
            MotionTableId = weenie.GetProperty(PropertyDataId.MotionTable) ?? 0;
            PhysicsTableId = weenie.GetProperty(PropertyDataId.PhysicsEffectTable) ?? 0;
            SoundTableId = weenie.GetProperty(PropertyDataId.SoundTable) ?? 0;
            Placement = (Placement?)weenie.GetProperty(PropertyInt.Placement);
            ObjScale = (float?)weenie.GetProperty(PropertyFloat.DefaultScale);
            Name = weenie.GetProperty(PropertyString.Name);
            // TODO: REMOVE ME?

        }

        public override ActivationResult CheckUseRequirements(WorldObject activator)
        {
            if (!(activator is Player player))
                return new ActivationResult(false);

            if (!(House.RootHouse.HouseHooksVisible ?? true) && Item != null && (!(Item is Hooker || Item is Book)))
            {
                if (House.RootHouse.HouseOwner.HasValue && (player.Guid.Full == House.RootHouse.HouseOwner.Value || player.House != null && player.House.HouseOwner == House.RootHouse.HouseOwner))
                    return new ActivationResult(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.ItemUnusableOnHook_CanOpen, Name));
                else
                    return new ActivationResult(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.ItemUnusableOnHook_CannotOpen, Name));
            }

            if (!(House.RootHouse.HouseHooksVisible ?? true) && Item != null)
            {
                // redirect to item.CheckUseRequirements
                return Item.CheckUseRequirements(activator);
            }

            if (!House.RootHouse.HouseOwner.HasValue || House.RootHouse.HouseOwner == 0 || (player.Guid.Full != House.RootHouse.HouseOwner.Value && player.House != null && player.House.HouseOwner != House.RootHouse.HouseOwner)) // Only HouseOwners can open hooks to add/remove items
            {
                if (Item == null)
                    return new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.HookItemNotUsable_CannotOpen));
                else if (Item != null && !(Item is Hooker))
                     return new ActivationResult(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.ItemUnusableOnHook_CannotOpen, Name));
                else
                    return new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.YouAreNotPermittedToUseThatHook));
            }

            if (!(House.RootHouse.HouseHooksVisible ?? true) && Item == null && House.RootHouse.HouseOwner > 0 && (player.Guid.Full == House.RootHouse.HouseOwner.Value || player.House != null && player.House.HouseOwner == House.RootHouse.HouseOwner)) // Only HouseOwners can open hooks to add/remove items, but hooks must be visible
            {
                return new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.HookItemNotUsable_CanOpen));
            }

            return new ActivationResult(true);
        }

        public override void ActOnUse(WorldObject wo)
        {
            if (!(House.HouseHooksVisible ?? true) && Item != null)
            {
                if (wo is Player player)
                    player.LasUsedHookId = Guid;

                // redirect to item.ActOnUse
                Item.OnActivate(wo);

                return;
            }

            base.ActOnUse(wo);
        }

        protected override void OnInitialInventoryLoadCompleted()
        {
            var hidden = !(House.HouseHooksVisible ?? true);

            Ethereal = !HasItem;
            if (!HasItem)
            {
                NoDraw = hidden;
                UiHidden = hidden;
            }
            else
            {
                NoDraw = false;
                UiHidden = false;
            }

            if (Inventory.Count > 0)
                OnAddItem();
        }

        /// <summary>
        /// This event is raised when player adds item to hook
        /// </summary>
        protected override void OnAddItem()
        {
            //Console.WriteLine("Hook.OnAddItem()");

            var item = Inventory.Values.FirstOrDefault();

            if (item == null)
            {
                log.Error("OnAddItem() raised for Hook but Inventory collection has no values.");
                return;
            }

            NoDraw = false;
            UiHidden = false;
            Ethereal = false;

            SetupTableId = item.SetupTableId;
            MotionTableId = item.MotionTableId;
            PhysicsTableId = item.PhysicsTableId;
            SoundTableId = item.SoundTableId;
            ObjScale = item.ObjScale;
            Name = item.Name;

            if (MotionTableId != 0)
                CurrentMotionState = new Motion(MotionStance.Invalid);

            Placement = (Placement)(item.HookPlacement ?? (int)ACE.Entity.Enum.Placement.Hook);

            item.EmoteManager.SetProxy(this);

            // Here we explicitly save the hook to the database to prevent item loss.
            // If the player adds an item to the hook, and the server crashes before the hook has been saved, the item will be lost.
            SaveBiotaToDatabase();

            EnqueueBroadcast(new GameMessageUpdateObject(this));
        }

        private static readonly ConcurrentDictionary<uint, WorldObject> cachedHookReferences = new ConcurrentDictionary<uint, WorldObject>();

        /// <summary>
        /// This event is raised when player removes item from hook
        /// </summary>
        protected override void OnRemoveItem(WorldObject removedItem)
        {
            //Console.WriteLine("Hook.OnRemoveItem()");

            if (!cachedHookReferences.TryGetValue(WeenieClassId, out var hook))
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);
                hook = WorldObjectFactory.CreateWorldObject(weenie, ObjectGuid.Invalid);

                cachedHookReferences[WeenieClassId] = hook;
            }

            SetupTableId = hook.SetupTableId;
            MotionTableId = hook.MotionTableId;
            PhysicsTableId = hook.PhysicsTableId;
            SoundTableId = hook.SoundTableId;
            Placement = hook.Placement;
            ObjScale = hook.ObjScale;
            Name = hook.Name;

            NoDraw = false;
            UiHidden = false;
            Ethereal = true;

            if (MotionTableId == 0)
                CurrentMotionState = null;

            removedItem.EmoteManager.ClearProxy();

            EnqueueBroadcast(new GameMessageUpdateObject(this));

            if (Inventory.Count < 1)
            {
                // Here we explicitly save the storage to the database to prevent property desync.
                SaveBiotaToDatabase();
            }
        }

        public override MotionCommand MotionPickup
        {
            get
            {
                var hookType = (HookType)(HookType ?? 0);

                switch (hookType)
                {
                    default:
                        return MotionCommand.Pickup;

                    case ACE.Entity.Enum.HookType.Wall:
                        return MotionCommand.Pickup10;

                    case ACE.Entity.Enum.HookType.Ceiling:
                    case ACE.Entity.Enum.HookType.Roof:
                        return MotionCommand.Pickup20;
                }
            }
        }

        public void UpdateHookVisibility()
        {
            if (!HasItem)
            {
                if (!(House.HouseHooksVisible ?? false))
                {
                    NoDraw = true;
                    UiHidden = true;
                    Ethereal = true;
                }
                else
                {
                    NoDraw = false;
                    UiHidden = false;
                    Ethereal = true;
                }

                EnqueueBroadcastPhysicsState();
                EnqueueBroadcast(new GameMessagePublicUpdatePropertyBool(this, PropertyBool.UiHidden, UiHidden));
            }
        }
    }
}
