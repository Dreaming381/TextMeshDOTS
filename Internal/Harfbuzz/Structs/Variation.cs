using System.Runtime.InteropServices;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Variation
    {        
        public uint axisTag;
        public float value;
    }
}
