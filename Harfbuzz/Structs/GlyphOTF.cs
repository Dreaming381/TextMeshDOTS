using System.Runtime.InteropServices;
using Unity.Entities;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    [InternalBufferCapacity(0)]
    public struct GlyphOTF : IBufferElementData
    {
        internal GlyphTable.Key glyphKey;
        public uint cluster;
        public int xAdvance;
        public int yAdvance;
        public int xOffset;
        public int yOffset;
    }
}
