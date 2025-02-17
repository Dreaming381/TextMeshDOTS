using System.Runtime.InteropServices;
using Unity.Entities;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    [InternalBufferCapacity(0)]
    public struct GlyphOTF : IBufferElementData
    {
        public Entity fontEntity;
        public uint codepoint;
        public uint cluster;
        public int xAdvance;
        public int yAdvance;
        public int xOffset;
        public int yOffset;
    }
}
