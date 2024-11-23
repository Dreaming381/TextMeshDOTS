using System.Runtime.InteropServices;
using Unity.Entities;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphInfo : IBufferElementData
    {
        public uint codepoint;
        public uint mask;
        public uint cluster;
        public uint var1;
        public uint var2;
    }
    //[StructLayout(LayoutKind.Sequential)]
    //public struct GlyphInfo : IBufferElementData
    //{
    //    public uint codepoint;
    //    private uint mask;
    //    public uint cluster;
    //    private uint var1;
    //    private uint var2;
    //}
}
