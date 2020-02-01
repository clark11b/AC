
using ACE.Entity;

namespace ACE.Server.WorldObjects
{
    public class Caster : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Caster(ACE.Entity.Models.Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Caster(Database.Models.Shard.Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }


        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Caster(ACE.Entity.Models.Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }
    }
}
