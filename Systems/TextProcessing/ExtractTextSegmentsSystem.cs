using UnityEngine;
using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using HarfBuzz;
using Font = HarfBuzz.Font;
using Buffer = HarfBuzz.Buffer;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using TextmeshDOTS;

namespace TextMeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateBefore(typeof(ShapeSystem))]
    //[DisableAutoCreation]
    public partial struct ExtractTextSegmentsSystem : ISystem
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {            
            m_query = SystemAPI.QueryBuilder()
                      .WithAll<FontBlobReference>()
                      .WithAll<CalliByteRaw>()
                      .WithAllRW<CalliByte>()
                      .WithAllRW<TextSpan>()
                      .WithAll<TextBaseConfiguration>()
                      .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByteRaw>());
            m_query.AddChangedVersionFilter(ComponentType.ReadWrite<TextBaseConfiguration>());
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_query.IsEmpty)
                return;

            //Debug.Log("Extract TextSpans");
            state.Dependency = new ExtractTextSegmentsJob
            {
                fontBlobReferenceLookup = SystemAPI.GetComponentLookup<FontBlobReference>(true),
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}

