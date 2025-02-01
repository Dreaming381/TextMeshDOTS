using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Unity.Collections;
using TextMeshDOTS.HarfBuzz;
using System;
using Buffer = TextMeshDOTS.HarfBuzz.Buffer;
using TextMeshDOTS.Rendering;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    public partial struct ShapeJob : IJobChunk
    {
        [ReadOnly] public ProfilerMarker marker;
        [ReadOnly] public ProfilerMarker marker2;

        public BufferTypeHandle<GlyphOTF> glyphOTFHandle;

        [ReadOnly] public NativeArray<Entity> fontEntities;
        [ReadOnly] public NativeArray<FontAssetRef> fontEntitiesLookup;
        [ReadOnly] public EntityTypeHandle entitesHandle;
        [ReadOnly] public BufferTypeHandle<AdditionalFontMaterialEntity> additionalFontMaterialEntityHandle;
        [ReadOnly] public ComponentTypeHandle<FontBlobReference> fontBlobReferenceHandle;
        [ReadOnly] public ComponentLookup<FontBlobReference> fontBlobReferenceLookup;
        [ReadOnly] public ComponentLookup<NativeFontPointer> nativeFontPointerLookup;
        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<TextSpan> textSpanHandle;
        [ReadOnly] public BufferLookup<UsedGlyphs> glyphsInUseLookup;
        public NativeList<FontEntityGlyph>.ParallelWriter missingGlyphs;

        public uint lastSystemVersion;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref calliByteHandle, lastSystemVersion) ||
                  chunk.DidChange(ref textSpanHandle, lastSystemVersion) ||
                  chunk.DidChange(ref fontBlobReferenceHandle, lastSystemVersion)))
                return;

            //Debug.Log("Shape job");
            var entities = chunk.GetNativeArray(entitesHandle);
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var textSpanBuffers = chunk.GetBufferAccessor(ref textSpanHandle);
            var glyphOTFBuffers = chunk.GetBufferAccessor(ref glyphOTFHandle);

            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));
            //var language = new Language(HB.HB_TAG('A', 'P', 'P', 'H'));
            var latinLTR = new SegmentProperties(Direction.LTR, Script.LATIN, language);
            var buffer = new Buffer(true);
            var features = new NativeList<Feature>(16, Allocator.Temp);

            //optional
            var additionalFontMaterialEntityBuffers = chunk.GetBufferAccessor(ref additionalFontMaterialEntityHandle);

            FontAssetArray fontAssetArray = default;
            bool hasMultipleFonts = additionalFontMaterialEntityBuffers.Length > 0;

            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var rootFontMaterialEntity = entities[indexInChunk];
                var textSpans = textSpanBuffers[indexInChunk];

                if (textSpans.Length == 0)
                    continue;//not ready yet

                var glyphOTFs = glyphOTFBuffers[indexInChunk];
                var calliBytes = calliBytesBuffers[indexInChunk];

                if (hasMultipleFonts)
                    fontAssetArray.Initialize(rootFontMaterialEntity, additionalFontMaterialEntityBuffers[indexInChunk], ref fontBlobReferenceLookup);
                else
                    fontAssetArray.Initialize(fontBlobReferenceLookup[rootFontMaterialEntity].value);

                var fontAssetRefs = fontAssetArray.fontAssetRefs;
                glyphOTFs.Clear();
                var text = calliBytes.Reinterpret<byte>();                

                int cur = 0;
                var currentSpan = textSpans[cur];
                uint startIndex;
                uint endIndex;
                do
                {
                    startIndex = currentSpan.startIndex;
                    int currentFont;
                    do
                    {
                        currentFont = currentSpan.fontMaterialIndex;
                        endIndex = currentSpan.endIndex;
                        cur++;
                    } while (cur < textSpans.Length && (currentSpan = textSpans[cur]).fontMaterialIndex == currentFont);

                    var length = (int)(endIndex - startIndex);
                    buffer.AddText(text, startIndex, length);
                    buffer.SetSegmentProperties(latinLTR);
                    //a number of white spaces are regretably not replaced by "space", no need to be handled in 
                    //https://github.com/harfbuzz/harfbuzz/commit/81ef4f407d9c7bd98cf62cef951dc538b13442eb#commitcomment-9469767
                    buffer.BufferFlag = BufferFlag.REMOVE_DEFAULT_IGNORABLES | BufferFlag.BOT | BufferFlag.EOT;
                    

                    //To-Do: revisit how to add features per textSpan
                    for (int i = 0, ii = textSpans.Length; i < ii; i++)
                    {
                        var textSpan = textSpans[i];
                        if ((textSpan.fontStyle & FontStyles.SmallCaps) == FontStyles.SmallCaps)
                            features.Add(new Feature(HB.HB_TAG('s', 'm', 'c', 'p'), 1, textSpan.startIndex, textSpan.endIndex));
                        if ((textSpan.fontStyle & FontStyles.Subscript) == FontStyles.Subscript)
                            features.Add(new Feature(HB.HB_TAG('s', 'u', 'b', 's'), 1, textSpan.startIndex, textSpan.endIndex));
                        if ((textSpan.fontStyle & FontStyles.Superscript) == FontStyles.Superscript)
                            features.Add(new Feature(HB.HB_TAG('s', 'u', 'p', 's'), 1, textSpan.startIndex, textSpan.endIndex));
                    }
                    features.Add(new Feature() { tag = HB.HB_TAG('f', 'r', 'a', 'c'), value = 1, start = 0, end = (uint)calliBytes.Length, });

                    var fontAssetRef = fontAssetRefs[currentFont];
                    var fontEntityID = fontEntitiesLookup.IndexOf(fontAssetRef);
                    var fontEntity = fontEntities[fontEntityID];
                    var nativeFontPointer = nativeFontPointerLookup[fontEntity];
                    var font = nativeFontPointer.font;

                    var glyphsInUse = glyphsInUseLookup[fontEntity].AsNativeArray().Reinterpret<uint>();
                    


                    font.Shape(buffer, features);

                    //var glyphInfos = buffer.GlyphInfo();
                    //var glyphPositions = buffer.GlyphPositions();
                    var glyphInfos = buffer.GetGlyphInfosSpan();
                    var glyphPositions = buffer.GetGlyphPositionsSpan();
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
                        if (!glyphsInUse.Contains(codepoint))
                            missingGlyphs.AddNoResize(new FontEntityGlyph { entity = fontEntity, glyphID = codepoint });
                    }
                    buffer.ClearContent();
                    features.Clear();
                } while (cur < textSpans.Length);
            }
            buffer.Dispose();
        }
    }
}
