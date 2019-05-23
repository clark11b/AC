﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.Shard
{
    public partial class BiotaPropertiesIID
    {
        public uint Id { get; set; }
        public uint ObjectId { get; set; }
        public ushort Type { get; set; }
        public uint Value { get; set; }
        public long? TypeValueCombined { get; set; }

        public Biota Object { get; set; }
    }
}
