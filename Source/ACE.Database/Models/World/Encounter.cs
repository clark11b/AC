﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World
{
    public partial class Encounter
    {
        public int Landblock { get; set; }
        public uint WeenieClassId { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
    }
}
