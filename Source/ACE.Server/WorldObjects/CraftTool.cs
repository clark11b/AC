
using ACE.Entity;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects
{
    public class CraftTool : Stackable
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public CraftTool(ACE.Entity.Models.Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public CraftTool(Database.Models.Shard.Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            if (PetDevice.IsEncapsulatedSpirit(this) && target is PetDevice petDevice)
            {
                petDevice.Refill(player, this);
                return;
            }

            if (Aetheria.IsAetheriaManaStone(this) && Aetheria.IsAetheria(target.WeenieClassId))
            {
                Aetheria.UseObjectOnTarget(player, this, target);
                return;
            }

            // fallback on recipe manager
            base.HandleActionUseOnTarget(player, target);
        }

        public override void ActOnUse(WorldObject wo)
        {
            // Do nothing
        }
    }
}
