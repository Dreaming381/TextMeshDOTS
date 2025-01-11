using TextMeshDOTS.Rendering;
using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using TextMeshDOTS.HarfBuzz;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]    
    public partial struct GenerateRenderGlyphsJob : IJobChunk
    {
        public BufferTypeHandle<RenderGlyph> renderGlyphHandle;
        public BufferTypeHandle<GlyphMappingElement> glyphMappingElementHandle;
        public BufferTypeHandle<FontMaterialSelectorForGlyph> selectorHandle;
        public ComponentTypeHandle<TextRenderControl> textRenderControlHandle;

        [ReadOnly] public NativeHashMap<FontAssetRef, Entity> fontEntities;
        [ReadOnly] public EntityTypeHandle entitesHandle;
        [ReadOnly] public BufferTypeHandle<AdditionalFontMaterialEntity> additionalFontMaterialEntityHandle;
        [ReadOnly] public ComponentTypeHandle<FontBlobReference> fontBlobReferenceHandle;
        [ReadOnly] public ComponentLookup<FontBlobReference> fontBlobReferenceLookup;

        [ReadOnly] public ComponentLookup<DynamicFontAsset> dynamicFontAssetsLookup;
        [ReadOnly] public ComponentTypeHandle<GlyphMappingMask> glyphMappingMaskHandle;
        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<GlyphOTF> glyphOTFHandle;
        [ReadOnly] public BufferTypeHandle<TextSpan> textSpanHandle;
        [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> textBaseConfigurationHandle;


        public uint lastSystemVersion;

        private GlyphMappingWriter m_glyphMappingWriter;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref glyphMappingMaskHandle, lastSystemVersion) ||
                  chunk.DidChange(ref calliByteHandle, lastSystemVersion) ||
                  chunk.DidChange(ref textSpanHandle, lastSystemVersion) ||
                  chunk.DidChange(ref textBaseConfigurationHandle, lastSystemVersion) ||
                  chunk.DidChange(ref fontBlobReferenceHandle, lastSystemVersion)))
                return;

            //Debug.Log("Generate Glyphs");
            var entities = chunk.GetNativeArray(entitesHandle);
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var glyphOTFBuffers = chunk.GetBufferAccessor(ref glyphOTFHandle);
            var textSpanBuffers = chunk.GetBufferAccessor(ref textSpanHandle);
            var renderGlyphBuffers = chunk.GetBufferAccessor(ref renderGlyphHandle);
            var glyphMappingBuffers = chunk.GetBufferAccessor(ref glyphMappingElementHandle);
            var glyphMappingMasks = chunk.GetNativeArray(ref glyphMappingMaskHandle);
            var textBaseConfigurations = chunk.GetNativeArray(ref textBaseConfigurationHandle);
            var textRenderControls = chunk.GetNativeArray(ref textRenderControlHandle);           
            

            // Optional
            var selectorBuffers = chunk.GetBufferAccessor(ref selectorHandle);
            var additionalFontMaterialEntityBuffers = chunk.GetBufferAccessor(ref additionalFontMaterialEntityHandle);

            FontAssetArray fontAssetArray = default;
            bool hasMultipleFonts = selectorBuffers.Length > 0 && additionalFontMaterialEntityBuffers.Length > 0;
            //var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            //while(enumerator.NextEntityIndex(out var i))
            //{ }
            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var rootFontMaterialEntity = entities[indexInChunk];
                var calliBytes = calliBytesBuffers[indexInChunk];
                var glyphOTFs = glyphOTFBuffers[indexInChunk];
                var textSpans = textSpanBuffers[indexInChunk];
                var renderGlyphs = renderGlyphBuffers[indexInChunk];
                var textBaseConfiguration = textBaseConfigurations[indexInChunk];
                var textRenderControl = textRenderControls[indexInChunk];

                if (textSpans.Length == 0)
                    continue;//not ready yet
                 
                m_glyphMappingWriter.StartWriter(glyphMappingMasks.Length > 0 ? glyphMappingMasks[indexInChunk].mask : default);

                DynamicBuffer<FontMaterialSelectorForGlyph> m_selectorBuffer=default;
                if (selectorBuffers.Length > 0)
                {                    
                    m_selectorBuffer = selectorBuffers[indexInChunk];
                    m_selectorBuffer.Clear();
                }

                if (hasMultipleFonts)
                    fontAssetArray.Initialize(rootFontMaterialEntity, additionalFontMaterialEntityBuffers[indexInChunk], ref fontBlobReferenceLookup);
                else
                    fontAssetArray.Initialize(fontBlobReferenceLookup[rootFontMaterialEntity].value);

                GlyphGeneration.CreateRenderGlyphs(ref fontAssetArray, fontEntities,
                                                   dynamicFontAssetsLookup,
                                                   ref m_selectorBuffer,
                                                   ref renderGlyphs,
                                                   ref m_glyphMappingWriter,                                                   
                                                   in calliBytes,
                                                   in glyphOTFs,
                                                   in textSpans,
                                                   in textBaseConfiguration,
                                                   hasMultipleFonts);

                if (glyphMappingBuffers.Length > 0)
                {
                    var mapping = glyphMappingBuffers[indexInChunk];
                    m_glyphMappingWriter.EndWriter(ref mapping, renderGlyphs.Length);
                }

                textRenderControl.flags = TextRenderControl.Flags.Dirty;
                textRenderControls[indexInChunk] = textRenderControl;
            }
        }
    }
}
