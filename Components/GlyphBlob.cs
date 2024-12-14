
using UnityEngine.TextCore;

namespace TextMeshDOTS
{
    public struct GlyphBlob
    {
        public uint glyphID;
        public GlyphExtents glyphExtents;   //source: UnityFontAsset or Harfbuzz (GlyphExtends)
        public GlyphRect glyphRect;         //source: UnityFontAsset or build self (width and height from GlyphExtends, x and y from atlas slot, adjusted for padding)
    }

    //public struct GlyphSizeComparer : IComparer<HBGlyph>
    //{
    //    public int Compare(HBGlyph a, HBGlyph b)
    //    {
    //        if (a.size == b.size)
    //        {
    //            return 0;
    //        }
    //        else
    //        {
    //            if (a.size > b.size)
    //                return 1;
    //            else
    //                return -1;
    //        }
    //    }
    //}
    //public struct GlyphWidthComparer : IComparer<HBGlyph>
    //{
    //    public int Compare(HBGlyph a, HBGlyph b)
    //    {
    //        if (a.glyphExtents.width == b.glyphExtents.width)
    //        {
    //            return 0;
    //        }
    //        else
    //        {
    //            if (a.glyphExtents.width > b.glyphExtents.width)
    //                return 1;
    //            else
    //                return -1;
    //        }
    //    }
    //}
}
