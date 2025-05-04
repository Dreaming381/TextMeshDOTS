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
        public ComponentTypeHandle<TextRenderControl> textRenderControlHandle;

        [ReadOnly] public NativeArray<FontAssetRef> fontEntitiesLookup;
        [ReadOnly] public EntityTypeHandle entitesHandle;
        [ReadOnly] public BufferTypeHandle<AdditionalFontMaterialEntity> additionalFontMaterialEntityHandle;
        [ReadOnly] public ComponentTypeHandle<FontBlobReference> fontBlobReferenceHandle;
        [ReadOnly] public ComponentLookup<FontBlobReference> fontBlobReferenceLookup;
        public Entity textColorGradientEntity;
        [ReadOnly] public BufferLookup<TextColorGradient> textColorGradientLookup;

        [ReadOnly] public ComponentLookup<DynamicFontAsset> dynamicFontAssetsLookup;
        [ReadOnly] public ComponentLookup<FontAssetRef> fontAssetRefLookup;
        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<GlyphOTF> glyphOTFHandle;
        [ReadOnly] public BufferTypeHandle<XMLTag> xmlTagHandle;
        [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> textBaseConfigurationHandle;


        public uint lastSystemVersion;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref calliByteHandle, lastSystemVersion) ||
                  chunk.DidChange(ref xmlTagHandle, lastSystemVersion) ||
                  chunk.DidChange(ref textBaseConfigurationHandle, lastSystemVersion) ||
                  chunk.DidChange(ref fontBlobReferenceHandle, lastSystemVersion)))
                return;

            //Debug.Log("Generate glyphs job");
            var entities = chunk.GetNativeArray(entitesHandle);
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var glyphOTFBuffers = chunk.GetBufferAccessor(ref glyphOTFHandle);
            var xmlTagBuffers = chunk.GetBufferAccessor(ref xmlTagHandle);
            var renderGlyphBuffers = chunk.GetBufferAccessor(ref renderGlyphHandle);
            var textBaseConfigurations = chunk.GetNativeArray(ref textBaseConfigurationHandle);
            var textRenderControls = chunk.GetNativeArray(ref textRenderControlHandle);

            TextColorGradientArray textColorGradientArray = default;
            textColorGradientArray.Initialize(textColorGradientEntity, textColorGradientLookup);

            // Optional
            var additionalFontMaterialEntityBuffers = chunk.GetBufferAccessor(ref additionalFontMaterialEntityHandle);

            FontAssetArray fontAssetArray = default;
            bool hasMultipleFonts = additionalFontMaterialEntityBuffers.Length > 0;

            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var rootFontMaterialEntity = entities[indexInChunk];
                var calliBytes = calliBytesBuffers[indexInChunk];
                var glyphOTFs = glyphOTFBuffers[indexInChunk];
                var xmlTags = xmlTagBuffers[indexInChunk];
                var renderGlyphs = renderGlyphBuffers[indexInChunk];
                var textBaseConfiguration = textBaseConfigurations[indexInChunk];
                var textRenderControl = textRenderControls[indexInChunk];
                 
                if (hasMultipleFonts)
                    fontAssetArray.Initialize(rootFontMaterialEntity, additionalFontMaterialEntityBuffers[indexInChunk], ref fontBlobReferenceLookup);
                else
                    fontAssetArray.Initialize(fontBlobReferenceLookup[rootFontMaterialEntity].value);


                GlyphGeneration.CreateRenderGlyphs(ref fontAssetArray, 
                                                   ref dynamicFontAssetsLookup,
                                                   ref fontAssetRefLookup,
                                                   ref renderGlyphs,
                                                   in calliBytes,
                                                   in glyphOTFs,
                                                   in xmlTags,
                                                   in textBaseConfiguration,
                                                   ref textColorGradientArray);

                textRenderControl.flags = TextRenderControl.Flags.Dirty;
                textRenderControls[indexInChunk] = textRenderControl;
            }
        }
    }
}
