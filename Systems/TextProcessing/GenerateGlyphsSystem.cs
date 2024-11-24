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
    [UpdateAfter(typeof(UpdateAtlasSystem))]
    public partial struct GenerateGlyphsSystem : ISystem
    {
        EntityQuery m_query;

        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = SystemAPI.QueryBuilder()
                      .WithAll<FontBlobReference>()
                      .WithAll<NativeFont>()
                      .WithAllRW<RenderGlyph>()
                      .WithAll<CalliByte>()
                      .WithAll<GlyphOTF>()
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
                additionalEntitiesHandle = SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(true),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                nativeFontReferenceHandle = SystemAPI.GetComponentTypeHandle<NativeFont>(true),
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

