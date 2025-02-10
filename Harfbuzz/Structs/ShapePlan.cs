using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace TextMeshDOTS.HarfBuzz
{
    public struct ShapePlan : IDisposable
    {
        public IntPtr ptr;
        public ShapePlan(Face face, ref SegmentProperties props, NativeList<Feature> features, IntPtr shaper_list)
        {
            unsafe
            {
                ptr = HB.hb_shape_plan_create_cached(face.ptr, ref props, (IntPtr)features.GetUnsafePtr(), (uint)features.Length, shaper_list);
            }            
        }
        public void Execute(Font font, Buffer buffer, NativeList<Feature> features)
        {
            unsafe
            {
                HB.hb_shape_plan_execute(ptr, font, buffer, (IntPtr)features.GetUnsafePtr(), (uint)features.Length);
            }
        }

        public void Dispose()
        {
            HB.hb_shape_plan_destroy(ptr);
        }
    }    
}