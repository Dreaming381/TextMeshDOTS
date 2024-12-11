using System.Collections.Generic;
using UnityEngine;

namespace HarfBuzz
{
    public struct HBGlyph
    {
        public uint glyphID;
        public RectInt glyphRect;
        public int size => glyphRect.height * glyphRect.width;
    }
    public struct GlyphSizeComparer : IComparer<HBGlyph>
    {
        public int Compare(HBGlyph a, HBGlyph b)
        {
            if (a.size == b.size)
            {
                return 0;
            }
            else
            {
                if (a.size > b.size)
                    return 1;
                else
                    return -1;
            }
        }
    }
    public struct GlyphWidthComparer : IComparer<HBGlyph>
    {
        public int Compare(HBGlyph a, HBGlyph b)
        {
            if (a.glyphRect.width == b.glyphRect.width)
            {
                return 0;
            }
            else
            {
                if (a.glyphRect.width > b.glyphRect.width)
                    return 1;
                else
                    return -1;
            }
        }
    }
}
