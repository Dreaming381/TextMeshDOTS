using HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;


namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(RegisterFontMaterialSystem))]    
    //[DisableAutoCreation]
    public partial struct ExtractTextSegmentsSystem : ISystem
    {
        EntityQuery m_query;
        EntityQuery fontEntityQ;
        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {            
            m_query = SystemAPI.QueryBuilder()
                      .WithAll<CalliByteRaw>()
                      .WithAllRW<CalliByte>()
                      .WithAllRW<TextSpan>()
                      .WithAll<TextBaseConfiguration>()
                      .Build();
            fontEntityQ = SystemAPI.QueryBuilder()
                  .WithAll<UsedGlyphs>()
                  .WithAll<DynamicFontAssets>()
                  .Build();

            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByteRaw>());
            //m_query.AddChangedVersionFilter(ComponentType.ReadWrite<FontMaterial>());
            //m_query.AddChangedVersionFilter(ComponentType.ReadWrite<TextBaseConfiguration>());

            state.RequireForUpdate(fontEntityQ);
            state.RequireForUpdate<FontHashMap>();
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //if (m_query.IsEmpty)
            //    return;
            var fontHashMap = SystemAPI.GetSingleton<FontHashMap>();
            if (fontHashMap.fontsDirty == true)
                return;
            
            var fontEntities = fontHashMap.fontEntities;

            state.Dependency = new ExtractTextSegmentsChunkJob
            {                
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(false),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(false),

                fontEntities = fontEntities,
                entitesHandle = SystemAPI.GetEntityTypeHandle(),
                additionalFontMaterialEntityHandle =  SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                fontBlobReferenceLookup =  SystemAPI.GetComponentLookup<FontBlobReference>(true),
                calliByteRawHandle = SystemAPI.GetBufferTypeHandle<CalliByteRaw>(true),                
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}

