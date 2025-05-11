
using TextMeshDOTS.HarfBuzz;
using UnityEngine.TextCore;

namespace TextMeshDOTS
{
    public struct GlyphBlob
    {
        internal GlyphTable.Entry entry;
        public GlyphRect glyphRect;         //source: UnityFontAsset or build self (width and height from GlyphExtends, x and y from atlas slot, adjusted for padding)
    }
}
