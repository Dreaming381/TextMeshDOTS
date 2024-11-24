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
    [UpdateAfter(typeof(LoadNativeFont))]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    //[DisableAutoCreation]
    public partial struct ShapeSystem : ISystem
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
                      .WithAllRW<GlyphOTF>()
                      .WithAll<CalliByte>()
                      .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByte>());
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_query.IsEmpty)
                return;
            state.Dependency = new ShapeJob
            {
                marker = marker,
                marker2 = marker2,

                GlyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(false),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(true),
                nativeFontReferenceHandle = SystemAPI.GetComponentTypeHandle<NativeFont>(true),
                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            //}.Schedule(m_query, state.Dependency);
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}

