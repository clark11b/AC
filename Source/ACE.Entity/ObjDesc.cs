using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Models;

namespace ACE.Entity
{
    public class ObjDesc
    {
        public uint PaletteID { get; set; }
        public List<SubPalette> SubPalettes { get; set; } = new List<SubPalette>();
        public List<TextureMapChange> TextureChanges { get; set; } = new List<TextureMapChange>();
        public List<PropertiesAnimPart> AnimPartChanges { get; set; } = new List<PropertiesAnimPart>();

        /// <summary>
        /// Helper function to ensure we don't add redundant parts to the list
        /// </summary>
        public void AddTextureChange(TextureMapChange tm)
        {
            var e = TextureChanges.FirstOrDefault(c => c.PartIndex == tm.PartIndex && c.OldTexture == tm.OldTexture && c.NewTexture == tm.NewTexture);
            if (e == null)
                TextureChanges.Add(tm);
        }

        /// <summary>
        /// Helper function to ensure we only have one AnimationPartChange.PartId in the list
        /// </summary>
        public void AddAnimPartChange(PropertiesAnimPart ap)
        {
            var p = AnimPartChanges.FirstOrDefault(c => c.Index == ap.Index && c.AnimationId == ap.AnimationId);
            if (p != null)
                AnimPartChanges.Remove(p);
            AnimPartChanges.Add(ap);
        }
    }
}
