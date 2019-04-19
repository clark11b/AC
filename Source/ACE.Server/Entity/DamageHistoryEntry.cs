using System;

using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class DamageHistoryEntry
    {
        public WeakReference<WorldObject> DamageSource;

        public DamageType DamageType;
        public int Amount;

        public uint CurrentHealth;
        public uint MaxHealth;

        public DateTime Time;

        /// <summary>
        /// Constructs a new entry for the DamageHistory
        /// </summary>
        /// <param name="damageSource">The attacker or source of the damage</param>
        /// <param name="amount">A negative amount for damage taken, positive for healing</param>
        public DamageHistoryEntry(Creature creature, WorldObject damageSource, DamageType damageType, int amount)
        {
            if (damageSource != null)
                DamageSource = new WeakReference<WorldObject>(damageSource);

            DamageType = damageType;
            Amount = amount;

            CurrentHealth = creature.Health.Current;
            MaxHealth = creature.Health.MaxValue;

            Time = DateTime.UtcNow;
        }
    }
}
