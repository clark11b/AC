﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World
{
    public partial class WeeniePropertiesSkill
    {
        public uint ObjectId { get; set; }
        public ushort Type { get; set; }
        public ushort LevelFromPP { get; set; }
        public ushort AdjustPP { get; set; }
        public uint SAC { get; set; }
        public uint PP { get; set; }
        public uint InitLevel { get; set; }
        public uint ResistanceAtLastCheck { get; set; }
        public double LastUsedTime { get; set; }

        public Weenie Object { get; set; }
    }
}
