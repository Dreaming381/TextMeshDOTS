using System.Runtime.InteropServices;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphPosition
    {
        public int xAdvance;
        public int yAdvance;
        public int xOffset;
        public int yOffset;
        private uint var1;
    }
}
