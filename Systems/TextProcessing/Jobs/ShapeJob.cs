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

        public BufferTypeHandle<GlyphOTF> GlyphOTFHandle;
       

        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<TextSpan> textSpanHandle;
        [NativeDisableUnsafePtrRestriction][ReadOnly] public ComponentTypeHandle<NativeFont> nativeFontReferenceHandle;

        public uint lastSystemVersion;


        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!chunk.DidChange(ref calliByteHandle, lastSystemVersion))
                return;

            var textSpanBuffers = chunk.GetBufferAccessor(ref textSpanHandle);
            if (textSpanBuffers.Length == 0) //nothing to shape
                return;

            var GlyphOTFBuffers = chunk.GetBufferAccessor(ref GlyphOTFHandle);
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            
            var nativeFontReferences = chunk.GetNativeArray(ref nativeFontReferenceHandle);


            var language = new Language(HB.HB_TAG('e', 'n', 'g', ' '));
            var buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);
            var segmentProperties = new SegmentProperties();
            //unsafe
            //{
            //    buffer.GetSegmentProperties(&segmentProperties);
            //}
            //Debug.Log(props.script);
            //Debug.Log(props.direction);
            //Debug.Log(props.language.ToString());
            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var glyphOTFs = GlyphOTFBuffers[indexInChunk];
                var calliBytes = calliBytesBuffers[indexInChunk];
                var textSpans = textSpanBuffers[indexInChunk];
                var nativeFontReference = nativeFontReferences[indexInChunk];
                var font = nativeFontReference.nativeFont;

                glyphOTFs.Clear();
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


                //marker.Begin();
                unsafe
                {
                    //HB.hb_shape(HBfont.ptr, buffer.ptr, IntPtr.Zero, 0);
                    HB.hb_shape(font.ptr, buffer.ptr, features.Length > 0 ? (IntPtr)features.GetUnsafePtr() : IntPtr.Zero, (uint)features.Length);
                    //HB.hb_shape(font.ptr, buffer.ptr, (IntPtr)features.GetUnsafePtr(), (uint)features.Length);
                }
                //marker.End();

                var glyphInfos = buffer.GlyphInfo();
                var glyphPositions = buffer.GlyphPositions();
                for (int i = 0, ii = glyphInfos.Length; i < ii; i++) 
                {
                    var glyphInfo = glyphInfos[i];
                    var glyphPosition = glyphPositions[i];
                    glyphOTFs.Add(new GlyphOTF 
                    {
                        codepoint= glyphInfo.codepoint, 
                        cluster = glyphInfo.cluster,
                        xAdvance = glyphPosition.xAdvance,
                        yAdvance = glyphPosition.yAdvance,
                        xOffset = glyphPosition.xOffset,
                        yOffset = glyphPosition.yOffset, 
                    });
                }


                buffer.ClearContent();
                //unsafe
                //{
                //    buffer.SetSegmentProperties(&segmentProperties);
                //}
                buffer.Language = language.ptr;
                buffer.Script = Script.Latin;
                buffer.Direction = Direction.LeftToRight;
            }
            buffer.Dispose();
        }
    }
}
