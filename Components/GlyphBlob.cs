
using UnityEngine.TextCore;

namespace TextMeshDOTS
{
    public struct GlyphBlob
    {
        public uint glyphID;
        public GlyphExtents glyphExtents;   //source: UnityFontAsset or Harfbuzz (GlyphExtends)
        public GlyphRect glyphRect;         //source: UnityFontAsset or build self (width and height from GlyphExtends, x and y from atlas slot, adjusted for padding)
    }
}
