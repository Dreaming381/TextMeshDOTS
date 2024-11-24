using TextMeshDOTS.Rendering;
using TextMeshDOTS;
using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Profiling;
using Unity.Collections;
using HarfBuzz;

namespace TextmeshDOTS
{
    [BurstCompile]    
    public partial struct GenerateRenderGlyphsJob : IJobChunk
    {
        public BufferTypeHandle<RenderGlyph> renderGlyphHandle;
        public BufferTypeHandle<GlyphMappingElement> glyphMappingElementHandle;
        public BufferTypeHandle<FontMaterialSelectorForGlyph> selectorHandle;
        public ComponentTypeHandle<TextRenderControl> textRenderControlHandle;

        [ReadOnly] public ComponentTypeHandle<GlyphMappingMask> glyphMappingMaskHandle;
        [ReadOnly] public BufferTypeHandle<CalliByte> calliByteHandle;
        [ReadOnly] public BufferTypeHandle<GlyphOTF> glyphOTFHandle;
        [ReadOnly] public BufferTypeHandle<TextSpan> textSpanHandle;
        [ReadOnly] public ComponentTypeHandle<TextBaseConfiguration> textBaseConfigurationHandle;
        [NativeDisableUnsafePtrRestriction][ReadOnly] public ComponentTypeHandle<NativeFont> nativeFontReferenceHandle;
        [ReadOnly] public ComponentTypeHandle<FontBlobReference> fontBlobReferenceHandle;
        [ReadOnly] public BufferTypeHandle<AdditionalFontMaterialEntity> additionalEntitiesHandle;
        [ReadOnly] public ComponentLookup<FontBlobReference> fontBlobReferenceLookup;

        public uint lastSystemVersion;

        private GlyphMappingWriter m_glyphMappingWriter;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref glyphMappingMaskHandle, lastSystemVersion) ||
                  chunk.DidChange(ref calliByteHandle, lastSystemVersion) ||
                  chunk.DidChange(ref textBaseConfigurationHandle, lastSystemVersion) ||
                  chunk.DidChange(ref fontBlobReferenceHandle, lastSystemVersion)))
                return;

            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var glyphOTFBuffers = chunk.GetBufferAccessor(ref glyphOTFHandle);
            var textSpanBuffers = chunk.GetBufferAccessor(ref textSpanHandle);
            var renderGlyphBuffers = chunk.GetBufferAccessor(ref renderGlyphHandle);
            var glyphMappingBuffers = chunk.GetBufferAccessor(ref glyphMappingElementHandle);
            var glyphMappingMasks = chunk.GetNativeArray(ref glyphMappingMaskHandle);
            var textBaseConfigurations = chunk.GetNativeArray(ref textBaseConfigurationHandle);
            var fontBlobReferences = chunk.GetNativeArray(ref fontBlobReferenceHandle);
            var nativeFontReferences = chunk.GetNativeArray(ref nativeFontReferenceHandle);            
            var textRenderControls = chunk.GetNativeArray(ref textRenderControlHandle);

            // Optional
            var selectorBuffers = chunk.GetBufferAccessor(ref selectorHandle);
            var additionalEntitiesBuffers = chunk.GetBufferAccessor(ref additionalEntitiesHandle);
            bool hasMultipleFonts = selectorBuffers.Length > 0 && additionalEntitiesBuffers.Length > 0;

            FontMaterialSet fontMaterialSet = default;

            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var calliBytes = calliBytesBuffers[indexInChunk];
                var glyphOTFs = glyphOTFBuffers[indexInChunk];
                var textSpans = textSpanBuffers[indexInChunk];
                var renderGlyphs = renderGlyphBuffers[indexInChunk];
                var fontBlobReference = fontBlobReferences[indexInChunk];
                var nativeFontReference = nativeFontReferences[indexInChunk];
                var textBaseConfiguration = textBaseConfigurations[indexInChunk];
                var textRenderControl = textRenderControls[indexInChunk];

                m_glyphMappingWriter.StartWriter(glyphMappingMasks.Length > 0 ? glyphMappingMasks[indexInChunk].mask : default);
                if (hasMultipleFonts)
                    fontMaterialSet.Initialize(fontBlobReference.blob, selectorBuffers[indexInChunk], additionalEntitiesBuffers[indexInChunk], ref fontBlobReferenceLookup);
                else
                    fontMaterialSet.Initialize(fontBlobReference.blob);

                GlyphGeneration.CreateRenderGlyphs(ref nativeFontReference,
                                                   ref fontMaterialSet,
                                                   ref renderGlyphs,
                                                   ref m_glyphMappingWriter,                                                   
                                                   in calliBytes,
                                                   in glyphOTFs,
                                                   in textSpans,
                                                   in textBaseConfiguration);

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
