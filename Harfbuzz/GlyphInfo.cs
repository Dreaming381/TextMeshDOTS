using System.Runtime.InteropServices;

namespace HarfBuzz
{

    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphInfo
    {
        public uint codepoint;
        private uint mask;
        public uint cluster;
        private uint var1;
        private uint var2;
    }
}
