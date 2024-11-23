using TextMeshDOTS.Rendering;
using TextMeshDOTS;
using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Unity.Collections;
using HarfBuzz;
using System;
using UnityEngine;
using Buffer = HarfBuzz.Buffer;
using UnityEngine.TextCore.Text;

namespace TextmeshDOTS
{
    [BurstCompile]
    public partial struct ShapeJob : IJobChunk
    {
        [ReadOnly] public ProfilerMarker marker;
        [ReadOnly] public ProfilerMarker marker2;

        public BufferTypeHandle<GlyphInfo> glyphInfoHandle;
        public BufferTypeHandle<GlyphPosition> glyphPositionHandle;
        

        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<TextSpan> textSpanHandle;
        [NativeDisableUnsafePtrRestriction][ReadOnly] public ComponentTypeHandle<NativeFontReference> nativeFontReferenceHandle;

        public uint lastSystemVersion;


        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!chunk.DidChange(ref calliByteHandle, lastSystemVersion))
                return;
            
            var glyphInfoBuffers = chunk.GetBufferAccessor(ref glyphInfoHandle);
            var glyphPositionBuffers = chunk.GetBufferAccessor(ref glyphPositionHandle);
            

            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var textSpanBuffers = chunk.GetBufferAccessor(ref textSpanHandle);
            var nativeFontReferences = chunk.GetNativeArray(ref nativeFontReferenceHandle);


            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var glyphInfos = glyphInfoBuffers[indexInChunk];
                var glyphPositions = glyphPositionBuffers[indexInChunk];                
                var calliBytes = calliBytesBuffers[indexInChunk];
                var textSpans = textSpanBuffers[indexInChunk];
                var nativeFontReference = nativeFontReferences[indexInChunk];
                var font = nativeFontReference.nativeFont;


                glyphInfos.Clear();
                glyphPositions.Clear();
                var buffer = new Buffer(Direction.LeftToRight, Script.Latin);
                buffer.AddText(calliBytes.Reinterpret<byte>());

                var features = new NativeList<Feature>(16, Allocator.Temp);
                for(int i  = 0, length= textSpans.Length; i < length; i++) 
                {
                    var textSpan = textSpans[i];
                    if ((textSpan.fontStyle & FontStyles.SmallCaps) == FontStyles.SmallCaps)
                        features.Add(new Feature() { tag = HB.HB_TAG('s', 'm', 'c', 'p'), value = 1, start = (uint)textSpan.startIndex, end = (uint)(textSpan.startIndex + textSpan.length), });
                    //if ((textSpan.fontStyle & FontStyles.Subscript) == FontStyles.Subscript)
                    //    features.Add(new Feature() { tag = HB.HB_TAG('s', 'u', 'b', 's'), value = 1, start = (uint)textSpan.startIndex, end = (uint)(textSpan.startIndex + textSpan.length), });
                    //if ((textSpan.fontStyle & FontStyles.Superscript) == FontStyles.Superscript)
                    //    features.Add(new Feature() { tag = HB.HB_TAG('s', 'u', 'p', 's'), value = 1, start = (uint)textSpan.startIndex, end = (uint)(textSpan.startIndex + textSpan.length), });
                }
                //features.Add(new Feature() { tag = HB.HB_TAG('f', 'r', 'a', 'c'), value = 1, start = 0, end = (uint)calliBytes.Length, });

                var lang = new FixedString32Bytes("en");
                unsafe
                {
                    var language = HB.hb_language_from_string(lang.GetUnsafePtr(), -1);
                    buffer.Language = language;
                }

                unsafe
                {
                    //HB.hb_shape(HBfont.ptr, buffer.ptr, IntPtr.Zero, 0);
                    HB.hb_shape(font.ptr, buffer.ptr, features.Length > 0 ? (IntPtr)features.GetUnsafePtr() : IntPtr.Zero, (uint)features.Length);
                    //HB.hb_shape(font.ptr, buffer.ptr, (IntPtr)features.GetUnsafePtr(), (uint)features.Length);
                }
                marker.End();

                glyphInfos.AddRange(buffer.GlyphInfo());
                glyphPositions.AddRange(buffer.GlyphPositions());

                buffer.Dispose();               
            }
        }
    }
}
