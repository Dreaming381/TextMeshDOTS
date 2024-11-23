using System.Runtime.InteropServices;
using Unity.Entities;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphPosition : IBufferElementData
    {
        public int xAdvance;
        public int yAdvance;
        public int xOffset;
        public int yOffset;
        private uint var1;
    }
}
