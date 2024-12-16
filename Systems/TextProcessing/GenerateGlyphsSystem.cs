using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;
using HarfBuzz;

namespace TextMeshDOTS.TextProcessing
{
    //[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(UpdateAtlasSystem))]
    [UpdateAfter(typeof(NativeFontManagerSystem))]
    public partial struct GenerateGlyphsSystem : ISystem
    {
        EntityQuery m_query;

        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = SystemAPI.QueryBuilder()
                      .WithAllRW<RenderGlyph>()
                      .WithAll<CalliByte>()
                      .WithAll<GlyphOTF>()
                      .WithAll<TextSpan>()                      
                      .WithAll<TextBaseConfiguration>()
                      .WithAllRW<TextRenderControl>()
                      //.WithAll<FontMaterial>()
                      .WithAll<FontEntity>()
                      .Build();
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new GenerateRenderGlyphsJob
            {
                renderGlyphHandle = SystemAPI.GetBufferTypeHandle<RenderGlyph>(false),
                glyphMappingElementHandle = SystemAPI.GetBufferTypeHandle<GlyphMappingElement>(false),                
                selectorHandle = SystemAPI.GetBufferTypeHandle<FontMaterialSelectorForGlyph>(false),
                textRenderControlHandle = SystemAPI.GetComponentTypeHandle<TextRenderControl>(false),

                //fontMaterialHandle = SystemAPI.GetBufferTypeHandle<FontMaterial>(true),
                fontEntityHandle = SystemAPI.GetBufferTypeHandle<FontEntity>(true),
                fontTextureReferenceLookup = SystemAPI.GetComponentLookup<FontTextureReference>(true),
                glyphMappingMaskHandle = SystemAPI.GetComponentTypeHandle<GlyphMappingMask>(true),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(true),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(true),
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),
               
                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion, 
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}