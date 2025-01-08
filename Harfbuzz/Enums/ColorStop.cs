using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorStop
    {
        public float offset;
        [MarshalAs(UnmanagedType.I1)]
        public bool isForeground;
        public ColorARGB color;
    }
    public struct ColorStopComparer : IComparer<ColorStop>
    {
        public int Compare(ColorStop a, ColorStop b)
        {
            return a.offset.CompareTo(b.offset);            
        }
    }
}
