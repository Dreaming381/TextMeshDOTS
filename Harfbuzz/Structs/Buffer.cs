using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace TextMeshDOTS.HarfBuzz
{
    public unsafe struct Buffer : IDisposable
    {
        public IntPtr ptr;
        public Buffer(Direction direction, Script script, Language language)
        {
            ptr = HB.hb_buffer_create();
            HB.hb_buffer_set_direction(ptr, direction);
            HB.hb_buffer_set_script(ptr, script);
            HB.hb_buffer_set_language(ptr, language);
        }
        public Buffer(bool dummyProperty)
        {
            ptr = HB.hb_buffer_create();
        }
        public Direction Direction {
            get { return HB.hb_buffer_get_direction(ptr); }
            set { HB.hb_buffer_set_direction(ptr, value); } 
        }
        public Script Script {
            get { return HB.hb_buffer_get_script(ptr); }
            set { HB.hb_buffer_set_script(ptr, value); } 
        }
        public Language Language
        {
            get => HB.hb_buffer_get_language(ptr);
            set => HB.hb_buffer_set_language(ptr, value);
        }
        public BufferFlag BufferFlag
        {
            get { return HB.hb_buffer_get_flags(ptr); }
            set { HB.hb_buffer_set_flags(ptr, value); }
        }
        //public IntPtr Language
        //{
        //    get => HB.hb_buffer_get_language(ptr);
        //    set => HB.hb_buffer_set_language(ptr, value);
        //}
        //public string GetLanguageAsString()
        //{
        //    HB.hb_language_to_string(HB.hb_buffer_get_language(ptr));

        //}

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

        public void GetSegmentProperties(out SegmentProperties segmentProperties)
        {
            HB.hb_buffer_get_segment_properties(ptr, out segmentProperties);
        }
        public void SetSegmentProperties(ref SegmentProperties segmentProperties)
        {
            HB.hb_buffer_set_segment_properties(ptr, ref segmentProperties);
        }
        public uint Length => HB.hb_buffer_get_length(ptr);
        public void Add(uint codepoint, uint cluster)
        {
            if ((int)Length != 0 && (ContentType != ContentType.UNICODE))
                throw new InvalidOperationException("Non empty buffer's ContentType must be of type Unicode.");
            if (ContentType == ContentType.GLYPHS)
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
        public void AddText(DynamicBuffer<byte> text, uint startIndex, int length)
        {
            HB.hb_buffer_add_utf8(ptr, (byte*)text.GetUnsafeReadOnlyPtr(), text.Length, startIndex, length);
        }
        public void AddText(NativeArray<byte> text, uint startIndex, int length)
        {
            HB.hb_buffer_add_utf8(ptr, (byte*)text.GetUnsafeReadOnlyPtr(), text.Length, startIndex, length);
        }
        public void AddText(NativeText text, uint startIndex, int length)
        {
            HB.hb_buffer_add_utf8(ptr, (byte*)text.GetUnsafePtr(), text.Length, startIndex, length);
        }
        public void ClearContent()
        {
            HB.hb_buffer_clear_contents(ptr);
        }
        public bool AllocationSucessfull()
        {
            return HB.hb_buffer_allocation_successful(ptr);
        }
        public void Reset()
        {
            HB.hb_buffer_reset(ptr);
        }
        public void Dispose()
        {
            HB.hb_buffer_destroy(ptr);
        }

        public NativeArray<GlyphInfo> GetGlyphInfo()
        {
            uint length;
            var glyphInfoPtr = HB.hb_buffer_get_glyph_infos(ptr, out length);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<GlyphInfo>((void*)glyphInfoPtr, (int)length, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle<GlyphInfo>(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return result;
        }
        public unsafe ReadOnlySpan<GlyphInfo> GetGlyphInfosSpan()
        {
            uint length;
            var infoPtrs = HB.hb_buffer_get_glyph_infos(ptr, out length);
            return new ReadOnlySpan<GlyphInfo>(infoPtrs, (int)length);
        }

        public NativeArray<GlyphPosition> GetGlyphPositions()
        {
            uint length;
            var glyphInfoPtr = HB.hb_buffer_get_glyph_positions(ptr, out length);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<GlyphPosition>((void*)glyphInfoPtr, (int)length, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle<GlyphPosition>(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return result;
        }
        public unsafe ReadOnlySpan<GlyphPosition> GetGlyphPositionsSpan()
        {
            uint length;
            var infoPtrs = HB.hb_buffer_get_glyph_positions(ptr, out length);
            return new ReadOnlySpan<GlyphPosition>(infoPtrs, (int)length);
        }
    }

    
}