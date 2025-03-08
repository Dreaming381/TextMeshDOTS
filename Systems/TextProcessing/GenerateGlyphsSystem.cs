using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;
using TextMeshDOTS.HarfBuzz;

namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(UpdateAtlasSystem))]
    [UpdateAfter(typeof(UpdateFontAtlasSystem))]
    public partial struct GenerateGlyphsSystem : ISystem
    {
        EntityQuery m_query, fontstateQ, fontEntitiesQ;

        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontstateQ = SystemAPI.QueryBuilder()
                      .WithAll<FontState>()
                      .WithNone<FontsDirtyTag>()
                      .Build();
            m_query = SystemAPI.QueryBuilder()
                      .WithAllRW<RenderGlyph>()
                      .WithAll<CalliByte>()
                      .WithAll<GlyphOTF>()
                      .WithAll<XMLTag>()
                      .WithAll<TextBaseConfiguration>()
                      .WithAllRW<TextRenderControl>()
                      .Build();
            fontEntitiesQ = SystemAPI.QueryBuilder()
                .WithAll<FontAssetRef>()
                .WithAll<UsedGlyphs>()
                .WithAll<MissingGlyphs>()
                .WithAll<DynamicFontAsset>()
                .Build();
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<GlyphOTF>());
            state.RequireForUpdate(fontstateQ);
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_query.IsEmpty)
                return;
            //Debug.Log("Generate glyphs system");

            var fontEntities = fontEntitiesQ.ToEntityArray(state.WorldUpdateAllocator);
            var fontEntitiesLookup = fontEntitiesQ.ToComponentDataArray<FontAssetRef>(state.WorldUpdateAllocator);
            SystemAPI.TryGetSingletonEntity<TextColorGradient>(out Entity textColorGradientEntity);
            state.Dependency = new GenerateRenderGlyphsJob
            {
                renderGlyphHandle = SystemAPI.GetBufferTypeHandle<RenderGlyph>(false),
                glyphMappingElementHandle = SystemAPI.GetBufferTypeHandle<GlyphMappingElement>(false),
                textRenderControlHandle = SystemAPI.GetComponentTypeHandle<TextRenderControl>(false),

                fontEntities = fontEntities,
                fontEntitiesLookup = fontEntitiesLookup,
                entitesHandle = SystemAPI.GetEntityTypeHandle(),
                additionalFontMaterialEntityHandle = SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                fontBlobReferenceLookup = SystemAPI.GetComponentLookup<FontBlobReference>(true),
                dynamicFontAssetsLookup = SystemAPI.GetComponentLookup<DynamicFontAsset>(true),
                fontAssetRefLookup = SystemAPI.GetComponentLookup<FontAssetRef>(true),
                glyphMappingMaskHandle = SystemAPI.GetComponentTypeHandle<GlyphMappingMask>(true),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(true),
                xmlTagHandle = SystemAPI.GetBufferTypeHandle<XMLTag>(true),
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),

                textColorGradientEntity = textColorGradientEntity,
                textColorGradientLookup = SystemAPI.GetBufferLookup<TextColorGradient>(true),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}