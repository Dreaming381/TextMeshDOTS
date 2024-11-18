using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;


namespace HarfBuzz
{
    public unsafe struct Buffer : IDisposable
    {
        public IntPtr ptr;

        public Buffer(Direction direction, Script script)
        {
            ptr = HB.hb_buffer_create();
            HB.hb_buffer_set_direction(ptr, direction);
            HB.hb_buffer_set_script(ptr, script);
        }
        public Direction Direction {
            get { return HB.hb_buffer_get_direction(ptr); }
            set { HB.hb_buffer_set_direction(ptr, value); } 
        }
        public Script Script {
            get { return HB.hb_buffer_get_script(ptr); }
            set { HB.hb_buffer_set_script(ptr, value); } 
        }
        public IntPtr Language
        {
            get => HB.hb_buffer_get_language(ptr);
            set => HB.hb_buffer_set_language(ptr, value);
        }
        //public ContentType ContentType => HB.hb_buffer_get_content_type(ptr);
        public ContentType ContentType
        {
            get => HB.hb_buffer_get_content_type(ptr);
            set => HB.hb_buffer_set_content_type(ptr, value);
        }
        public ClusterLevel ClusterLevel
        {
            get => HB.hb_buffer_get_cluster_level(ptr);
            set => HB.hb_buffer_set_cluster_level(ptr, value);
        }
        public uint Length => HB.hb_buffer_get_length(ptr);
        public void Add(uint codepoint, uint cluster)
        {
            if ((int)Length != 0 && (ContentType != ContentType.Unicode))
                throw new InvalidOperationException("Non empty buffer's ContentType must be of type Unicode.");
            if (ContentType == ContentType.Glyphs)
                throw new InvalidOperationException("ContentType must not be of type Glyphs");

            HB.hb_buffer_add(ptr, codepoint, cluster);
        }

        public void AddText(string str)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
            fixed (byte* text = bytes)
            {
                HB.hb_buffer_add_utf8(ptr, text, bytes.Length, 0, bytes.Length);
            }
            
            //HB.hb_buffer_add_utf8(ptr, text, text.Length, 0, text.Length);
        }
        public void AddText(DynamicBuffer<byte> text)
        {
            HB.hb_buffer_add_utf8(ptr, (byte*)text.GetUnsafeReadOnlyPtr(), text.Length, 0, text.Length);
        }

        public void Dispose()
        {
            HB.hb_buffer_destroy(ptr);
        }

        //public GlyphInfo[] GlyphInfo()
        //{
        //    uint length;
        //    IntPtr glyphInfoPtr = HB.hb_buffer_get_glyph_infos(ptr, out length);
        //    var glyphInfos = new GlyphInfo[length];
        //    var size = Marshal.SizeOf(typeof(GlyphInfo));
        //    for (int i = 0; i < length; ++i)
        //    {
        //        glyphInfos[i] = Marshal.PtrToStructure<GlyphInfo>(glyphInfoPtr);
        //        glyphInfoPtr += size;
        //    }
        //    return glyphInfos;
        //}
        public NativeArray<GlyphInfo> GlyphInfo()
        {
            uint length;
            IntPtr glyphInfoPtr = HB.hb_buffer_get_glyph_infos(ptr, out length);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<GlyphInfo>((void*)glyphInfoPtr, (int)length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle<GlyphInfo>(ref result, AtomicSafetyHandle.Create());
#endif
            return result;
        }
        //public GlyphPosition[] GlyphPositions()
        //{
        //    uint length;
        //    IntPtr glyphPositionPtr = HB.hb_buffer_get_glyph_positions(ptr, out length);
        //    var glyphPositions = new GlyphPosition[length];
        //    var size = Marshal.SizeOf(typeof(GlyphPosition));
        //    for (int i = 0; i < length; ++i)
        //    {
        //        glyphPositions[i] = Marshal.PtrToStructure<GlyphPosition>(glyphPositionPtr);
        //        glyphPositionPtr += size;
        //    }
        //    return glyphPositions;
        //}
        public NativeArray<GlyphPosition> GlyphPositions()
        {
            uint length;
            IntPtr glyphInfoPtr = HB.hb_buffer_get_glyph_positions(ptr, out length);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<GlyphPosition>((void*)glyphInfoPtr, (int)length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle<GlyphPosition>(ref result, AtomicSafetyHandle.Create());
#endif
            return result;
        }
    }
}