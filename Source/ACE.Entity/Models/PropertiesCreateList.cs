using System;

namespace ACE.Entity.Models
{
    public class PropertiesCreateList
    {
        public sbyte DestinationType { get; set; }
        public uint WeenieClassId { get; set; }
        public int StackSize { get; set; }
        public sbyte Palette { get; set; }
        public float Shade { get; set; }
        public bool TryToBond { get; set; }

        public PropertiesCreateList Clone()
        {
            var result = new PropertiesCreateList
            {
                DestinationType = DestinationType,
                WeenieClassId = WeenieClassId,
                StackSize = StackSize,
                Palette = Palette,
                Shade = Shade,
                TryToBond = TryToBond,
            };

            return result;
        }
    }
}
