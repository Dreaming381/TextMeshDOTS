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

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    public partial struct ShapeJob : IJobChunk
    {
        [ReadOnly] public ProfilerMarker marker;
        [ReadOnly] public ProfilerMarker marker2;

        public BufferTypeHandle<GlyphOTF> glyphOTFHandle;
        public BufferTypeHandle<FontMaterialSelectorForGlyph> selectorHandle;

        [ReadOnly] public NativeArray<Entity> fontEntities;
        [ReadOnly] public NativeArray<FontAssetRef> fontAssetRefs;
        [ReadOnly] public EntityTypeHandle entitesHandle;
        [ReadOnly] public BufferTypeHandle<AdditionalFontMaterialEntity> additionalFontMaterialEntityHandle;
        [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> textBaseConfigurationHandle;
        [ReadOnly] public ComponentTypeHandle<FontBlobReference> fontBlobReferenceHandle;
        [ReadOnly] public ComponentLookup<FontBlobReference> fontBlobReferenceLookup;
        [ReadOnly] public ComponentLookup<NativeFontPointer> nativeFontPointerLookup;
        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<XMLTag> xmlTagHandle;
        [ReadOnly] public BufferLookup<UsedGlyphs> glyphsInUseLookup;
        public NativeList<FontEntityGlyph>.ParallelWriter missingGlyphs;

        public uint lastSystemVersion;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref textBaseConfigurationHandle, lastSystemVersion) ||
                  chunk.DidChange(ref xmlTagHandle, lastSystemVersion) ||
                  chunk.DidChange(ref fontBlobReferenceHandle, lastSystemVersion)))
                return;

            //Debug.Log("Shape job");
            var entities = chunk.GetNativeArray(entitesHandle);
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var xmlTagBuffers = chunk.GetBufferAccessor(ref xmlTagHandle);
            var glyphOTFBuffers = chunk.GetBufferAccessor(ref glyphOTFHandle);
            var textBaseConfigurations = chunk.GetNativeArray(ref textBaseConfigurationHandle);

            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));
            //var language = new Language(HB.HB_TAG('A', 'P', 'P', 'H'));
            var segmentProperties = new SegmentProperties(Direction.LTR, Script.LATIN, language);
            var buffer = new Buffer(true);
            var openTypeFeatures = new OpenTypeFeatureConfig(16, Allocator.Temp);            

            //shape plans can be cached..no use case found yet where there this makes a signifiant difference
            //var shaperList = HB.hb_shape_list_shapers();
            //var shapePlanCache = new NativeHashMap<FontAssetRef, ShapePlan>(16, Allocator.Temp);

            //optional
            var selectorBuffers = chunk.GetBufferAccessor(ref selectorHandle);
            var additionalFontMaterialEntityBuffers = chunk.GetBufferAccessor(ref additionalFontMaterialEntityHandle);

            FontAssetArray fontAssetArray = default;
            bool hasMultipleFonts = additionalFontMaterialEntityBuffers.Length > 0;
            var chunkMissingGlyphs = new NativeList<FontEntityGlyph>(1024, Allocator.Temp);
            var cleanedText = new NativeList<byte>(1024, Allocator.Temp);
            LayoutConfig2 layoutConfig2 =default;
            FontConfig fontConfiguration = default;

            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var rootFontMaterialEntity = entities[indexInChunk];
                var xmlTagBuffer = xmlTagBuffers[indexInChunk];
                var glyphOTFs = glyphOTFBuffers[indexInChunk];
                var calliBytesBuffer = calliBytesBuffers[indexInChunk].Reinterpret<byte>();
                var textBaseConfiguration = textBaseConfigurations[indexInChunk];                

                DynamicBuffer<FontMaterialSelectorForGlyph> m_selectorBuffer;
                if (hasMultipleFonts)
                {
                    m_selectorBuffer = selectorBuffers[indexInChunk];
                    m_selectorBuffer.Clear();
                    fontAssetArray.Initialize(rootFontMaterialEntity, additionalFontMaterialEntityBuffers[indexInChunk], ref fontBlobReferenceLookup);
                }
                else
                {
                    m_selectorBuffer = default;
                    fontAssetArray.Initialize(fontBlobReferenceLookup[rootFontMaterialEntity].value);
                }
                var calliString = new CalliString(calliBytesBuffer);
                var rawCharacters = calliString.GetEnumerator();


                fontConfiguration.Reset(textBaseConfiguration, ref fontAssetArray);
                layoutConfig2.Reset(textBaseConfiguration);

                glyphOTFs.Clear();
                var text = calliBytesBuffer.Reinterpret<byte>();

                var calliStringRaw = new CalliString(calliBytesBuffer);
                var cleanedString = new NativeText(calliBytesBuffer.Length, Allocator.Temp);


                if (xmlTagBuffer.Length==0) //text has no richtext tags, just search font requested by textBaseConfiguration and shape 
                {
                    //copy text into buffer used for shaping, convert case while doing so
                    while (rawCharacters.MoveNext())
                    {
                        var currentRune = rawCharacters.Current;
                        if ((layoutConfig2.m_fontStyles & FontStyles.UpperCase) == FontStyles.UpperCase)
                            cleanedString.Append(currentRune.ToUpper());
                        else if ((layoutConfig2.m_fontStyles & FontStyles.LowerCase) == FontStyles.LowerCase)
                            cleanedString.Append(currentRune.ToLower());
                        else
                            cleanedString.Append(currentRune);
                    }
                    //find font Entity requested by combination of font family and style
                    fontConfiguration.m_fontFamilyHash = fontAssetRefs[0].familyHash;                    
                    var fontIndex = fontConfiguration.GetFontIndex(ref fontAssetArray);                    
                    if (fontIndex != -1)
                        fontConfiguration.m_fontMaterialIndex = fontIndex;

                    openTypeFeatures.SetGlobalFeatures(textBaseConfiguration, (uint)text.Length);
                    Shape(buffer, cleanedString, 0, cleanedString.Length, ref segmentProperties, ref fontAssetArray, fontConfiguration.m_fontMaterialIndex, openTypeFeatures.values, glyphOTFs, hasMultipleFonts, m_selectorBuffer, chunkMissingGlyphs);
                    continue;
                }

                //text has richtext tags. Search segments where font, language, script and direction does does not change (To-Do: use ICU for that),
                //apply opentype features requested via richtext tags, and shape

                cleanedText.Capacity = calliStringRaw.Capacity;
                int tagsCounter = 0;
                XMLTag currentTag;

                int richTextStartID = 0;
                var currentFontMaterialIndex = fontConfiguration.m_fontMaterialIndex;

                //copy text into buffer used for shaping, convert case while doing so
                int nextTagPosition = xmlTagBuffer.Length > 0 ? xmlTagBuffer[tagsCounter].startID : calliString.Length;
                while (rawCharacters.MoveNext())
                {
                    var currentRune = rawCharacters.Current;
                    while (rawCharacters.NextRuneByteIndex > nextTagPosition)
                    {
                        if (tagsCounter < xmlTagBuffer.Length)
                        {
                            currentTag = xmlTagBuffer[tagsCounter++];
                            rawCharacters.GotoByteIndex(currentTag.endID);  // go to ">'
                            rawCharacters.MoveNext();                       // go to char after '>'
                            nextTagPosition = tagsCounter < xmlTagBuffer.Length ? xmlTagBuffer[tagsCounter].startID : calliString.Length;
                            layoutConfig2.Update(ref currentTag);
                            //Debug.Log($"{currentTag.tagType} {cleanedSegmentLength} {nextTagPositionInCleanedText}");
                        }
                        currentRune = rawCharacters.Current;
                        //continue;
                    }
                    if ((layoutConfig2.m_fontStyles & FontStyles.UpperCase) == FontStyles.UpperCase)
                        cleanedString.Append(currentRune.ToUpper());
                    else if ((layoutConfig2.m_fontStyles & FontStyles.LowerCase) == FontStyles.LowerCase)
                        cleanedString.Append(currentRune.ToLower());
                    else
                        cleanedString.Append(currentRune);
                }

                richTextStartID = 0;
                var cleanedEnd = 0;
                var cleanedStart = 0;
                tagsCounter = 0;
                while (cleanedStart < cleanedString.Length)
                {
                    while (tagsCounter < xmlTagBuffer.Length && fontConfiguration.m_fontMaterialIndex == currentFontMaterialIndex)
                    {
                        currentTag = xmlTagBuffer[tagsCounter];
                        var cleanedInterTagLength = (currentTag.startID - richTextStartID);
                        cleanedEnd += cleanedInterTagLength;
                        fontConfiguration.GetCurrentFontIndex(ref currentTag, ref fontAssetArray, ref calliStringRaw);
                        openTypeFeatures.Update(ref currentTag, cleanedEnd);
                        tagsCounter++;
                        richTextStartID = currentTag.endID + 1;
                    }
                    openTypeFeatures.FinalizeOpenTypeFeatures(cleanedText.Length);
                    openTypeFeatures.SetGlobalFeatures(textBaseConfiguration, (uint)cleanedText.Length);
                    var cleanedSegmentLength = cleanedEnd - cleanedStart;
                    Shape(buffer, cleanedString, cleanedStart, cleanedSegmentLength, ref segmentProperties, ref fontAssetArray, currentFontMaterialIndex, openTypeFeatures.values, glyphOTFs, hasMultipleFonts, m_selectorBuffer, chunkMissingGlyphs);
                    currentFontMaterialIndex = fontConfiguration.m_fontMaterialIndex;
                    cleanedStart = cleanedEnd;
                    if (tagsCounter == xmlTagBuffer.Length) //last loop in order to shape text between last tag and end of rich text buffer
                        cleanedEnd = cleanedString.Length;
                }
                cleanedString.Clear();
            }           
            //add missing glyphs identifed in chunks processed by this thread to missingGlyphs
            missingGlyphs.AddRangeNoResize(chunkMissingGlyphs);
            buffer.Dispose();
        }
        void Shape(Buffer buffer,
            //NativeArray<byte> text,
            NativeText text,
            int startIndex,
            int length,
            ref SegmentProperties segmentProperties, 
            ref FontAssetArray fontAssetArray,
            int fontMaterialIndex,
            NativeList<Feature> features,
            DynamicBuffer<GlyphOTF> glyphOTFs,
            bool hasMultipleFonts,
            DynamicBuffer<FontMaterialSelectorForGlyph> m_selectorBuffer,
            NativeList<FontEntityGlyph> chunkMissingGlyphs)
        {
            if (startIndex + length == text.Length && text[^1] == 0)
                length--; //last byte of CalliBytes buffer appears to be always '0', which should not be shaped. 
            buffer.AddText(text, (uint)startIndex, length);
            buffer.SetSegmentProperties(ref segmentProperties);

            //a number of white spaces are regretably not replaced by "space" (needs to be handled in GenerateGlyphJob)
            //https://github.com/harfbuzz/harfbuzz/commit/81ef4f407d9c7bd98cf62cef951dc538b13442eb#commitcomment-9469767
            buffer.BufferFlag = BufferFlag.REMOVE_DEFAULT_IGNORABLES | BufferFlag.BOT | BufferFlag.EOT;

            var fontAssetRef = fontAssetArray[fontMaterialIndex];
            var fontEntityID = fontAssetRefs.IndexOf(fontAssetRef);
            var fontEntity = fontEntities[fontEntityID];
            var nativeFontPointer = nativeFontPointerLookup[fontEntity];
            var font = nativeFontPointer.font;
            //if (!shapePlanCache.TryGetValue(fontAssetRef, out var shapePlan))
            //{                        
            //    shapePlan = new ShapePlan(nativeFontPointer.face, ref segmentProperties, features, shaperList);
            //    shapePlanCache.Add(fontAssetRef, shapePlan);
            //}
            //marker.Begin();
            //shapePlan.Execute(font, buffer, features);
            //marker.End();

            marker.Begin();
            font.Shape(buffer, features);
            marker.End();

            var glyphsInUse = glyphsInUseLookup[fontEntity].AsNativeArray().Reinterpret<uint>();
            var glyphInfos = buffer.GetGlyphInfosSpan();
            var glyphPositions = buffer.GetGlyphPositionsSpan();
            var capacity = glyphOTFs.Length + glyphInfos.Length;
            glyphOTFs.Capacity = capacity; //2x speedup compared to allocating for each element

            if (hasMultipleFonts)
            {
                m_selectorBuffer.Capacity = capacity;
                var fontMaterialSelectorForGlyph = new FontMaterialSelectorForGlyph { fontMaterialIndex = (byte)fontMaterialIndex };
                for (int i = 0, ii = glyphInfos.Length; i < ii; i++)
                    m_selectorBuffer.Add(fontMaterialSelectorForGlyph);
            }
            for (int i = 0, ii = glyphInfos.Length; i < ii; i++)
            {
                var glyphInfo = glyphInfos[i];
                var glyphPosition = glyphPositions[i];
                var codepoint = glyphInfo.codepoint;
                var glyphOTF = new GlyphOTF
                {
                    fontEntity = fontEntity,
                    codepoint = glyphInfo.codepoint,
                    cluster =  glyphInfo.cluster,
                    xAdvance = glyphPosition.xAdvance,
                    yAdvance = glyphPosition.yAdvance,
                    xOffset = glyphPosition.xOffset,
                    yOffset = glyphPosition.yOffset,
                };
                glyphOTFs.Add(glyphOTF);
                if (!glyphsInUse.Contains(codepoint))
                {
                    var fontEntityGlyph = new FontEntityGlyph { entity = fontEntity, glyphID = codepoint };
                    //we do not want to add redundantly the same glyph to missingGlyphs,
                    //so preferably we check if glyph has already been added. Does not work due to 
                    //ParrallelWriter. As a workaround, we create an additional list in this thread
                    //(just before chunk iteration starts), and check against that list
                    if (!chunkMissingGlyphs.Contains(fontEntityGlyph))
                        chunkMissingGlyphs.Add(fontEntityGlyph);
                }
            }
            buffer.ClearContent();
            features.Clear();
        }
    }
}
