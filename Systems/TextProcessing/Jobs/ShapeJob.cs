using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Unity.Collections;
using HarfBuzz;
using System;
using Buffer = HarfBuzz.Buffer;
using UnityEngine.TextCore.Text;
using TextMeshDOTS.Rendering;
using System.Linq;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    public partial struct ShapeJob : IJobChunk
    {
        [ReadOnly] public ProfilerMarker marker;
        [ReadOnly] public ProfilerMarker marker2;

        public BufferTypeHandle<GlyphOTF> glyphOTFHandle;
        public BufferTypeHandle<FontMaterialSelectorForGlyph> selectorHandle;

        [ReadOnly] public BufferTypeHandle<FontMaterial> fontMaterialHandle;
        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<TextSpan> textSpanHandle;
        [ReadOnly] public BufferLookup<GlyphsInUse> glyphsInUseLookup;
        public NativeList<FontEntityGlyph>.ParallelWriter missingGlyphs;

        public uint lastSystemVersion;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref calliByteHandle, lastSystemVersion) ||
                  chunk.DidChange(ref textSpanHandle, lastSystemVersion)))
                return;

            var fontMaterialBuffers = chunk.GetBufferAccessor(ref fontMaterialHandle);
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var textSpanBuffers = chunk.GetBufferAccessor(ref textSpanHandle);
            var glyphOTFBuffers = chunk.GetBufferAccessor(ref glyphOTFHandle);

            //var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));
            var language = new Language(HB.HB_TAG('A', 'P', 'P', 'H'));
            //var testLan1 = new Language("gla bla", -1);
            //var testLan2 = new Language("en-sdf");
            //Debug.Log($"{testLan1} {testLan2} {language}");
            var buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);
            //buffer.ClusterLevel = ClusterLevel.Characters;
            //var segmentProperties = new SegmentProperties();
            //unsafe
            //{
            //    buffer.GetSegmentProperties(&segmentProperties);
            //}
            //Debug.Log(props.script);
            //Debug.Log(props.direction);
            //Debug.Log($"{buffer.Language.ToString()}");
            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var textSpans = textSpanBuffers[indexInChunk];

                if (textSpans.Length == 0)
                    continue;//not ready yet

                var fontMaterial = fontMaterialBuffers[indexInChunk];
                var glyphOTFs = glyphOTFBuffers[indexInChunk];
                var calliBytes = calliBytesBuffers[indexInChunk];

                glyphOTFs.Clear();
                var text = calliBytes.Reinterpret<byte>();                

                var features = new NativeList<Feature>(16, Allocator.Temp);
                for(int i  = 0, length= textSpans.Length; i < length; i++) 
                {
                    var textSpan = textSpans[i];                    
                    if ((textSpan.fontStyle & FontStyles.SmallCaps) == FontStyles.SmallCaps)
                        features.Add(new Feature(HB.HB_TAG('s', 'm', 'c', 'p'), 1, textSpan.startIndex, textSpan.endIndex));
                    if ((textSpan.fontStyle & FontStyles.Subscript) == FontStyles.Subscript)
                        features.Add(new Feature(HB.HB_TAG('s', 'u', 'b', 's'), 1, textSpan.startIndex, textSpan.endIndex));
                    if ((textSpan.fontStyle & FontStyles.Superscript) == FontStyles.Superscript)
                        features.Add(new Feature(HB.HB_TAG('s', 'u', 'p', 's'), 1, textSpan.startIndex, textSpan.endIndex));
                }
                //features.Add(new Feature() { tag = HB.HB_TAG('f', 'r', 'a', 'c'), value = 1, start = 0, end = (uint)calliBytes.Length, });

                int cur = 0;
                var currentSpan = textSpans[cur];
                uint startIndex;
                uint endIndex;                

                do
                {
                    startIndex = currentSpan.startIndex;
                    endIndex = currentSpan.endIndex;
                    int currentFont;
                    do
                    {
                        currentFont = currentSpan.fontMaterialIndex;
                        endIndex = currentSpan.endIndex;
                        cur++;
                    } while (cur < textSpans.Length && (currentSpan = textSpans[cur]).fontMaterialIndex == currentFont);

                    var length = (int)(endIndex - startIndex);
                    buffer.AddText(text, startIndex, length);

                    var font = fontMaterial[currentFont].hbFont;
                    var fontEntity = fontMaterial[currentFont].fontEntity;
                    var glyphsInUse = glyphsInUseLookup[fontEntity].AsNativeArray().Reinterpret<uint>();
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
                        var codepoint = glyphInfo.codepoint;
                        glyphOTFs.Add(new GlyphOTF
                        {
                            codepoint = glyphInfo.codepoint,
                            cluster = glyphInfo.cluster,
                            xAdvance = glyphPosition.xAdvance,
                            yAdvance = glyphPosition.yAdvance,
                            xOffset = glyphPosition.xOffset,
                            yOffset = glyphPosition.yOffset,
                        });
                        if(!glyphsInUse.Contains(codepoint))
                            missingGlyphs.AddNoResize(new FontEntityGlyph{ entity = fontEntity, glyphID=codepoint});
                    }
                    buffer.ClearContent();
                    //unsafe
                    //{
                    //    buffer.SetSegmentProperties(&segmentProperties);
                    //}
                    buffer.Language = language;
                    buffer.Script = Script.Latin;
                    buffer.Direction = Direction.LeftToRight;
                } while (cur < textSpans.Length);
            }
            buffer.Dispose();
        }
    }
}
