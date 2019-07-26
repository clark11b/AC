﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.Shard
{
    public partial class CharacterPropertiesContract
    {
        public uint Id { get; set; }
        public uint CharacterId { get; set; }
        public uint ContractId { get; set; }
        public uint Version { get; set; }
        public uint Stage { get; set; }
        public ulong TimeWhenDone { get; set; }
        public ulong TimeWhenRepeats { get; set; }
        public bool DeleteContract { get; set; }
        public bool SetAsDisplayContract { get; set; }

        public virtual Character Character { get; set; }
    }
}
