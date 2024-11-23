using UnityEngine;
using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;
using TextmeshDOTS;
using HarfBuzz;

namespace TextMeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[DisableAutoCreation]
    [UpdateAfter(typeof(LoadNativeFont))]
    public partial struct GenerateGlyphsSystem : ISystem
    {
        EntityQuery m_query;
        static readonly ProfilerMarker marker = new ProfilerMarker("harfbuzz");
        static readonly ProfilerMarker marker2 = new ProfilerMarker("buffer");


        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = SystemAPI.QueryBuilder()
                      .WithAll<FontBlobReference>()
                      .WithAllRW<RenderGlyph>()
                      .WithAll<CalliByte>()
                      .WithAll<GlyphInfo>()
                      .WithAll<GlyphPosition>()
                      .WithAll<TextSpan>()
                      .WithAll<TextBaseConfiguration>()
                      .WithAllRW<TextRenderControl>()
                      .Build();
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new GenerateRenderGlyphsJob
            {
                marker = marker,
                marker2 = marker2,
                additionalEntitiesHandle = SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphInfoHandle = SystemAPI.GetBufferTypeHandle<GlyphInfo>(true),
                glyphPositionHandle = SystemAPI.GetBufferTypeHandle<GlyphPosition>(true),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                nativeFontReferenceHandle = SystemAPI.GetComponentTypeHandle<NativeFontReference>(true),
                fontBlobReferenceLookup = SystemAPI.GetComponentLookup<FontBlobReference>(true),
                glyphMappingElementHandle = SystemAPI.GetBufferTypeHandle<GlyphMappingElement>(false),
                glyphMappingMaskHandle = SystemAPI.GetComponentTypeHandle<GlyphMappingMask>(true),
                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
                renderGlyphHandle = SystemAPI.GetBufferTypeHandle<RenderGlyph>(false),
                selectorHandle = SystemAPI.GetBufferTypeHandle<FontMaterialSelectorForGlyph>(false),
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),
                textRenderControlHandle = SystemAPI.GetComponentTypeHandle<TextRenderControl>(false),
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}

