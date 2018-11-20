using ACE.Database.Models.Shard;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class SkillAlterationDevice : WorldObject
    {
        public SkillAlterationType TypeOfAlteration { get; set; }
        public Skill SkillToBeAltered { get; set; }

        public enum SkillAlterationType
        {
            Undef = 0,
            Specialize = 1,
            Lower = 2,
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public SkillAlterationDevice(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public SkillAlterationDevice(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            //Copy values to properties
            TypeOfAlteration = (SkillAlterationType)(GetProperty(PropertyInt.TypeOfAlteration) ?? 1);
            SkillToBeAltered = (Skill)(GetProperty(PropertyInt.SkillToBeAltered) ?? 0);
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item should be in the players possession.
        /// </summary>
        public override void UseItem(Player player)
        {
            var currentSkill = player.GetCreatureSkill(SkillToBeAltered);

            //Check to make sure we got a valid skill back
            if (currentSkill == null)
            {
                player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.YouFailToAlterSkill));
                return;
            }

            //Gather costs associated with manipulating currently selected skill
            var currentSkillCost = currentSkill.Skill.GetCost();

            switch (TypeOfAlteration)
            {
                case SkillAlterationType.Specialize:
                    //Check to make sure player won't exceed limit of 70 specialized credits after operation
                    if (currentSkillCost.SpecializationCost + GetTotalSpecializedCredits(player) > 70)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.TooManyCreditsInSpecializedSkills, currentSkill.Skill.ToSentence()));
                        player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.YouFailToAlterSkill));
                        break;
                    }

                    //Check to see if the skill is ripe for specializing
                    if (currentSkill.AdvancementClass == SkillAdvancementClass.Trained)
                    {
                        if (player.AvailableSkillCredits >= currentSkillCost.SpecializationCost)
                        {
                            if (player.SpecializeSkill(currentSkill.Skill, currentSkillCost.SpecializationCost, false))
                            {
                                //Specialization was successful, notify the client
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, currentSkill));
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
                                player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.YouHaveSucceededSpecializing_Skill, currentSkill.Skill.ToSentence()));
                                player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.None));

                                //Destroy the gem we used successfully
                                player.TryRemoveItemFromInventoryWithNetworkingWithDestroy(this, 1);

                                break;
                            }
                        }
                        else
                        {
                            player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.NotEnoughSkillCreditsToSpecialize, currentSkill.Skill.ToSentence()));
                            player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.YouFailToAlterSkill));
                            break;
                        }
                    }

                    //Tried to use a specialization gem on a skill that is either already specialized, or untrained
                    player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.Your_SkillMustBeTrained, currentSkill.Skill.ToSentence()));
                    player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.YouFailToAlterSkill));
                    break;
                case SkillAlterationType.Lower:
                    //We're using a Gem of Forgetfullness

                    //Check for equipped items that have requirements in the skill we're lowering
                    if (CheckWieldedItems(player))
                    {
                        //Items are wielded which might be affected by a lowering operation
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.CannotLowerSkillWhileWieldingItem, currentSkill.Skill.ToSentence()));
                        player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.YouFailToAlterSkill));
                        break;
                    }

                    if (currentSkill.AdvancementClass == SkillAdvancementClass.Specialized)
                    {
                        if (player.UnspecializeSkill(currentSkill.Skill, currentSkillCost.SpecializationCost))
                        {
                            //Unspecialization was successful, notify the client
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, currentSkill));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
                            player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.YouHaveSucceededUnspecializing_Skill, currentSkill.Skill.ToSentence()));
                            player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.None));

                            //Destroy the gem we used successfully
                            player.TryRemoveItemFromInventoryWithNetworkingWithDestroy(this, 1);

                            break;
                        }
                    }
                    else if (currentSkill.AdvancementClass == SkillAdvancementClass.Trained)
                    {
                        var untrainable = Player.IsSkillUntrainable(currentSkill.Skill);

                        if (player.UntrainSkill(currentSkill.Skill, currentSkillCost.TrainingCost))
                        {
                            //Untraining was successful, notify the client
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, currentSkill));
                            if (untrainable)
                            {
                                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
                                player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.YouHaveSucceededUntraining_Skill, currentSkill.Skill.ToSentence()));
                            }
                            else
                            {
                                player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.CannotUntrain_SkillButRecoveredXP, currentSkill.Skill.ToSentence()));
                            }
                            player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.None));

                            //Destroy the gem we used successfully
                            player.TryRemoveItemFromInventoryWithNetworkingWithDestroy(this, 1);

                            break;
                        }
                    }
                    else
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.Your_SkillIsAlreadyUntrained, currentSkill.Skill.ToSentence()));
                        player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.YouFailToAlterSkill));
                        break;
                    }
                    break;
                default:
                    player.Session.Network.EnqueueSend(new GameEventUseDone(player.Session, WeenieError.YouFailToAlterSkill));
                    break;
            }
        }

        /// <summary>
        /// Calculates and returns the current total number of specialized credits
        /// </summary>
        private int GetTotalSpecializedCredits(Player player)
        {
            var specializedCreditsTotal = 0;

            foreach (var skill in player.Skills.Keys)
            {
                var skillCost = skill.GetCost();
                var currentSkill = player.GetCreatureSkill(skill);

                if (currentSkill != null)
                {
                    if (currentSkill.AdvancementClass == SkillAdvancementClass.Specialized)
                    {
                        specializedCreditsTotal += skillCost.SpecializationCost;
                    }

                }
            }

            return specializedCreditsTotal;
        }

        /// <summary>
        /// Checks wielded items and their requirements to see if they'd be violated by an impending skill lowering operation
        /// </summary>
        private bool CheckWieldedItems(Player player)
        {
            foreach (var equippedItem in player.EquippedObjects.Values)
            {
                var itemWieldReq = (WieldRequirement)(equippedItem.GetProperty(PropertyInt.WieldRequirements) ?? 0);

                if (itemWieldReq == WieldRequirement.RawSkill || itemWieldReq == WieldRequirement.Skill)
                {
                    // Check WieldDifficulty property against player's Skill level, defined by item's WieldSkilltype property
                    var itemSkillReq = player.ConvertToMoASkill((Skill)(equippedItem.GetProperty(PropertyInt.WieldSkilltype) ?? 0));

                    if (itemSkillReq == SkillToBeAltered)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
