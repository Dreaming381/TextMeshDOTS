using System.Runtime.InteropServices;
using Unity.Entities;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphOTF : IBufferElementData
    {
        public uint codepoint;
        public uint cluster;
        public int xAdvance;
        public int yAdvance;
        public int xOffset;
        public int yOffset;
    }
}
