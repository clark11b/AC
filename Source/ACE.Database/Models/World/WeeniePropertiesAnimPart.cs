﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World
{
    public partial class WeeniePropertiesAnimPart
    {
        public uint ObjectId { get; set; }
        public byte Index { get; set; }
        public uint AnimationId { get; set; }

        public Weenie Object { get; set; }
    }
}
