using TextMeshDOTS.Rendering;
using TextMeshDOTS;
using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Collections;
using HarfBuzz;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]    
    public partial struct GenerateRenderGlyphsJob : IJobChunk
    {
        public BufferTypeHandle<RenderGlyph> renderGlyphHandle;
        public BufferTypeHandle<GlyphMappingElement> glyphMappingElementHandle;
        public BufferTypeHandle<FontMaterialSelectorForGlyph> selectorHandle;
        public ComponentTypeHandle<TextRenderControl> textRenderControlHandle;

        [ReadOnly] public BufferTypeHandle<FontMaterial> fontMaterialHandle;
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
                  chunk.DidChange(ref fontMaterialHandle, lastSystemVersion)))
                return;

            var fontMaterialBuffers = chunk.GetBufferAccessor(ref fontMaterialHandle);
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

            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var fontMaterial = fontMaterialBuffers[indexInChunk];
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

                GlyphGeneration.CreateRenderGlyphs(in fontMaterial,
                                                   ref m_selectorBuffer,
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
