﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World
{
    public partial class WeeniePropertiesString
    {
        public uint ObjectId { get; set; }
        public ushort Type { get; set; }
        public string Value { get; set; }

        public Weenie Object { get; set; }
    }
}
