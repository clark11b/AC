﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World
{
    public partial class WeeniePropertiesTextureMap
    {
        public uint ObjectId { get; set; }
        public byte Index { get; set; }
        public uint OldId { get; set; }
        public uint NewId { get; set; }

        public Weenie Object { get; set; }
    }
}
