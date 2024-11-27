using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;


namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateBefore(typeof(ShapeSystem))]
    //[DisableAutoCreation]
    public partial struct ExtractTextSegmentsSystem : ISystem
    {
        EntityQuery m_query;
        EntityQuery fontEntityQ;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {            
            m_query = SystemAPI.QueryBuilder()
                      .WithAll<CalliByteRaw>()
                      .WithAllRW<CalliByte>()
                      .WithAllRW<TextSpan>()
                      .WithAll<TextBaseConfiguration>()
                      .WithAll<FontMaterial>()
                      .Build();
            fontEntityQ = SystemAPI.QueryBuilder()
                  .WithAll<GlyphsInUse>()
                  .WithAll<HBFontAssetReference>()
                  .WithAll<DynamicFontBlobReference>()
                  .Build();

            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByteRaw>());
            m_query.AddChangedVersionFilter(ComponentType.ReadWrite<TextBaseConfiguration>());
            //m_query.AddChangedVersionFilter(ComponentType.ReadWrite<FontMaterial>());

            state.RequireForUpdate(fontEntityQ);
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //Debug.Log("Extract TextSpans");
            state.Dependency = new ExtractTextSegmentsJob
            {
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}

