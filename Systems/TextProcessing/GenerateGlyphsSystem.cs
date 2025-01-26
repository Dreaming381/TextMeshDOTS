using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;
using TextMeshDOTS.HarfBuzz;
using Codice.Client.BaseCommands;
using UnityEngine;

namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(UpdateAtlasSystem))]
    [UpdateAfter(typeof(UpdateFontAtlasSystem))]
    public partial struct GenerateGlyphsSystem : ISystem
    {
        EntityQuery m_query, fontsQ;

        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontsQ = SystemAPI.QueryBuilder()
                      .WithAll<FontState>()
                      .WithNone<FontsDirtyTag>()
                      .Build();
            m_query = SystemAPI.QueryBuilder()
                      .WithAllRW<RenderGlyph>()
                      .WithAll<CalliByte>()
                      .WithAll<GlyphOTF>()
                      .WithAll<TextSpan>()                      
                      .WithAll<TextBaseConfiguration>()
                      .WithAllRW<TextRenderControl>()
                      .Build();
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<GlyphOTF>());
            state.RequireForUpdate(fontsQ);
            SystemAPI.TryGetSingletonRW<FontHashMap>(out _);//still needed to create system dependency?
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_query.IsEmpty)
                return;
            //Debug.Log("Generate glyphs system");
            
            var fontHashMap = SystemAPI.GetSingleton<FontHashMap>();

            state.Dependency = new GenerateRenderGlyphsJob
            {
                renderGlyphHandle = SystemAPI.GetBufferTypeHandle<RenderGlyph>(false),
                glyphMappingElementHandle = SystemAPI.GetBufferTypeHandle<GlyphMappingElement>(false),                
                selectorHandle = SystemAPI.GetBufferTypeHandle<FontMaterialSelectorForGlyph>(false),
                textRenderControlHandle = SystemAPI.GetComponentTypeHandle<TextRenderControl>(false),

                fontEntities = fontHashMap.fontEntities,
                entitesHandle = SystemAPI.GetEntityTypeHandle(),
                additionalFontMaterialEntityHandle = SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                fontBlobReferenceLookup = SystemAPI.GetComponentLookup<FontBlobReference>(true),
                dynamicFontAssetsLookup = SystemAPI.GetComponentLookup<DynamicFontAsset>(true),
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