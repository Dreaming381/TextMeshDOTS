using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ColorLine
    {
        IntPtr ptr;

        public uint GetColorStops(uint start, ref NativeArray<ColorStop> colorStops)
        {
            uint count = (uint)colorStops.Length;
            var len = HB.hb_color_line_get_color_stops(ptr, 0, ref count, (IntPtr)colorStops.GetUnsafePtr());
            if (len > count)
            {
                Debug.Log("capacity was not sufficient, increasing");
                colorStops = colorStops=new NativeArray<ColorStop>((int)len, Allocator.Temp);
                HB.hb_color_line_get_color_stops(ptr, 0, ref len, (IntPtr)colorStops.GetUnsafePtr());
            }
            return len;
        }

        public PaintExtend GetExtend()
        {
            return HB.hb_color_line_get_extend(ptr);
        }
    };
    //struct hb_color_line_t
    //{
    //    void* data;

    //    hb_color_line_get_color_stops_func_t get_color_stops;
    //    void* get_color_stops_user_data;

    //    hb_color_line_get_extend_func_t get_extend;
    //    void* get_extend_user_data;

    //    void* reserved0;
    //    void* reserved1;
    //    void* reserved2;
    //    void* reserved3;
    //    void* reserved5;
    //    void* reserved6;
    //    void* reserved7;
    //    void* reserved8;
    //};
}
