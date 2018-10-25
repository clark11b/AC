using System;
using System.Collections.Generic;

using ACE.Server.Physics.Collision;

namespace ACE.Server.Physics.Entity
{
    public static class GfxObjCache
    {
        public static readonly Dictionary<uint, GfxObj> GfxObjs = new Dictionary<uint, GfxObj>();

        public static int Requests;
        public static int Hits;

        public static GfxObj Get(DatLoader.FileTypes.GfxObj _gfxObj)
        {
            Requests++;

            //if (Requests % 100 == 0)
            //Console.WriteLine($"GfxObjCache: Requests={Requests}, Hits={Hits}");

            if (GfxObjs.TryGetValue(_gfxObj.Id, out var result))
            {
                Hits++;
                return result;
            }

            // not cached, add it
            var gfxObj = new GfxObj(_gfxObj);
            GfxObjs.Add(_gfxObj.Id, gfxObj);
            return gfxObj;
        }
    }
}
