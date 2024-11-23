using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Feature
    {
        public uint tag;
        public uint value;
        public uint start;
        public uint end;
        public Feature(FixedString32Bytes feature)
        {           
            unsafe
            {
                var text = feature.GetUnsafePtr();
                bool result = HB.hb_feature_from_string(text, -1, out this);
            }
        }
    }
}
