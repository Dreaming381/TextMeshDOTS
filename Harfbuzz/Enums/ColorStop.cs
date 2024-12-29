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
}
