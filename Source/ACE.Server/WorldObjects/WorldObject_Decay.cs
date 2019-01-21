using System;
using System.Linq;

using ACE.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        /// <summary>
        /// The default number of seconds for a object on a landblock to disappear<para />
        /// Current default is 5 minutes
        /// </summary>
        protected TimeSpan DefaultTimeToRot { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// A decayable object is one that, when it exists on a landblock, would decay (rot) over time.<para />
        /// When it rots, it would be destroyed, and removed from the landblock.<para />
        /// In most cases, these should be player dropped items or corpses. It can also be a missile or spell projectile.<para />
        /// Items that have a TimeToRot value of -1 will return false.<para />
        /// Items that have a BaseDescriptionFlags with ObjectDescriptionFlag.Stuck set will return false.<para />
        /// Generators and items still linked to a generator will return false.
        /// </summary>
        public bool IsDecayable()
        {
            if (!TimeToRot.HasValue)
                return false;

            if (TimeToRot.HasValue && TimeToRot == -1)
                return false;

            if (OwnerId.HasValue || ContainerId.HasValue || WielderId.HasValue)
                return false;

            return true;
        }

        private bool decayCompleted;

        public void Decay(TimeSpan elapsed)
        {
            // http://asheron.wikia.com/wiki/Item_Decay

            if (decayCompleted)
                return;

            if (!TimeToRot.HasValue)
            {
                TimeToRot = DefaultTimeToRot.TotalSeconds;
                return;
            }

            var corpse = this as Corpse;

            if (corpse != null && corpse.Inventory.Count == 0 && TimeToRot.Value > Corpse.EmptyDecayTime)
            {
                TimeToRot = Corpse.EmptyDecayTime;
                return;
            }

            if (TimeToRot > 0)
            {
                TimeToRot -= elapsed.TotalSeconds;

                // Is there still time left?
                if (TimeToRot > 0)
                    return;

                TimeToRot = -2; // We force it to -2 to make sure it doesn't end up at 0 or -1. 0 indicates instant rot. -1 indicates no rot. 0 and -1 can be found in weenie defaults
            }

            if (this is Container container && container.IsOpen)
            {
                // If you wanted to add a grace period to the container to give Player B more time to open it after Player A closes it, it would go here.

                return;
            }

            // Time to rot has elapsed, time to disappear...
            decayCompleted = true;

            // If this is a player corpse, puke out the corpses contents onto the landblock
            if (corpse != null && !corpse.IsMonster)
            {
                var inventoryGUIDs = corpse.Inventory.Keys.ToList();

                foreach (var guid in inventoryGUIDs)
                {
                    if (corpse.TryRemoveFromInventory(guid, out var item))
                    {
                        item.Location = new Position(corpse.Location);
                        item.Placement = ACE.Entity.Enum.Placement.Resting; // This is needed to make items lay flat on the ground.
                        CurrentLandblock.AddWorldObject(item);
                    }
                }
            }

            if (corpse != null)
            {
                EnqueueBroadcast(new GameMessageScript(Guid, ACE.Entity.Enum.PlayScript.Destroy));

                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(1.0f);
                actionChain.AddAction(this, Destroy);
                actionChain.EnqueueChain();
            }
            else
                Destroy();
        }
    }
}
