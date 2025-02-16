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
        [ReadOnly] public BufferTypeHandle<CalliByteRaw> calliByteRawHandle; //required for fetching string values from richtext tags such as font names
        [ReadOnly] public BufferTypeHandle<XMLTag> xmlTagHandle;
        [ReadOnly] public BufferLookup<UsedGlyphs> glyphsInUseLookup;
        public NativeList<FontEntityGlyph>.ParallelWriter missingGlyphs;

        public uint lastSystemVersion;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref calliByteHandle, lastSystemVersion) ||
                  chunk.DidChange(ref xmlTagHandle, lastSystemVersion) ||
                  chunk.DidChange(ref fontBlobReferenceHandle, lastSystemVersion)))
                return;

            //Debug.Log("Shape job");
            var entities = chunk.GetNativeArray(entitesHandle);
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var calliBytesRawBuffers = chunk.GetBufferAccessor(ref calliByteRawHandle);
            var xmlTagBuffers = chunk.GetBufferAccessor(ref xmlTagHandle);
            var glyphOTFBuffers = chunk.GetBufferAccessor(ref glyphOTFHandle);
            var textBaseConfigurations = chunk.GetNativeArray(ref textBaseConfigurationHandle);

            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));
            //var language = new Language(HB.HB_TAG('A', 'P', 'P', 'H'));
            var segmentProperties = new SegmentProperties(Direction.LTR, Script.LATIN, language);
            var buffer = new Buffer(true);
            //var features = new NativeList<Feature>(16, Allocator.Temp);
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

            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var rootFontMaterialEntity = entities[indexInChunk];
                var xmlTags = xmlTagBuffers[indexInChunk];
                var glyphOTFs = glyphOTFBuffers[indexInChunk];
                var calliBytes = calliBytesBuffers[indexInChunk];
                var calliBytesRawBuffer = calliBytesRawBuffers[indexInChunk];
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

                FontConfig fontConfiguration = default;
                fontConfiguration.Reset(textBaseConfiguration, ref fontAssetArray);
                glyphOTFs.Clear();
                var text = calliBytes.Reinterpret<byte>();

                int fontMaterialIndex=0; 

                if (xmlTags.Length==0) //text has no richtext tags, just search font requested by textBaseConfiguration and shape 
                {
                    //find font Entity requested by combination of font family and style
                    var fontFamilyHash = fontAssetRefs[0].familyHash;
                    var desiredFontAssetRef = new FontAssetRef(fontConfiguration.fontFamilyHash, fontConfiguration.fontStyles);
                    var fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
                    fontMaterialIndex = fontIndex == -1 ? 0 : fontIndex;                   
                    Shape(buffer, text, 0, text.Length, ref segmentProperties, ref fontAssetArray, fontMaterialIndex, openTypeFeatures.values, glyphOTFs, hasMultipleFonts, m_selectorBuffer, chunkMissingGlyphs);
                    continue;
                }

                //text has richtext tags. Search segments where font, language, script and direction does does not change (To-Do: use ICU for that),
                //apply opentype features requested via richtext tags, and shape
                var calliStringRaw = new CalliString(calliBytesRawBuffer);


                int tagsCounter = 0;
                XMLTag currentTag;
                int startIndex = 0;
                int endIndex = text.Length;
                fontMaterialIndex = fontConfiguration.currentFontMaterialIndex;
                while (startIndex < endIndex)
                {
                    while (tagsCounter < xmlTags.Length && fontConfiguration.currentFontMaterialIndex == fontMaterialIndex)
                    {
                        currentTag = xmlTags[tagsCounter];
                        fontConfiguration.GetCurrentFontIndex(ref currentTag, ref fontAssetArray, ref calliStringRaw);
                        openTypeFeatures.UpdateOpenTypeFeatures(ref currentTag);
                        endIndex = currentTag.position;                        
                        tagsCounter++;
                    }                    
                    var length = endIndex - startIndex;
                    openTypeFeatures.FinalizeOpenTypeFeatures((uint)endIndex);
                    Shape(buffer, text, startIndex, length, ref segmentProperties, ref fontAssetArray, fontMaterialIndex, openTypeFeatures.values, glyphOTFs, hasMultipleFonts, m_selectorBuffer, chunkMissingGlyphs);
                    startIndex = endIndex;
                    endIndex = text.Length;
                    fontMaterialIndex = fontConfiguration.currentFontMaterialIndex;
                    openTypeFeatures.Clear();
                }
            }
            //add missing glyphs identifed in chunks processed by this thread to missingGlyphs
            missingGlyphs.AddRangeNoResize(chunkMissingGlyphs);
            buffer.Dispose();
        }

        void Shape(Buffer buffer, 
            DynamicBuffer<byte> text,
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
                    cluster = glyphInfo.cluster,
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
