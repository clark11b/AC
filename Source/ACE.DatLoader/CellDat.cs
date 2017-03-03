﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.DatLoader
{
    public class CellDat : DatDatabase
    {
        public CellDat(string filePath) : base(filePath)
        {
        }

        public override int SectorSize
        {
            get { return 64 * sizeof(uint); }
        }
    }
}
