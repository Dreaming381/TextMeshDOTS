using System;
using System.Runtime.InteropServices;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Feature
    {
        public uint tag;
        public uint value;
        public uint start;
        public uint end;
    }
}
