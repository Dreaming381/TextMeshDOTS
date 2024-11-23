using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphExtents 
    {
        public int x_bearing;
        public int y_bearing;
        public int width;
        public int height;
    }
}
