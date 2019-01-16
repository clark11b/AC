﻿using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World
{
    public partial class RecipeModsString
    {
        public uint Id { get; set; }
        public uint RecipeModId { get; set; }
        public int Stat { get; set; }
        public string Value { get; set; }
        public int Enum { get; set; }
        public int Unknown1 { get; set; }

        public RecipeMod RecipeMod { get; set; }
    }
}
