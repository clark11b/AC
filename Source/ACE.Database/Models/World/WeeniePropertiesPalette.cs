﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World
{
    public partial class WeeniePropertiesPalette
    {
        public uint ObjectId { get; set; }
        public uint SubPaletteId { get; set; }
        public ushort Offset { get; set; }
        public ushort Length { get; set; }

        public Weenie Object { get; set; }
    }
}
