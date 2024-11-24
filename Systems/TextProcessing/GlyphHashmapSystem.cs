using HarfBuzz;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(ShapeSystem))]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    partial struct GlyphHashmapSystem : ISystem
    {
        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = SystemAPI.QueryBuilder()
                              .WithAll<GlyphOTF>()
                              .WithAll<FontAssetReference>()
                              .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<GlyphOTF>());

            var glyphAtlas = new FontAtlasInfo
            {
                glyphAtlas = new NativeParallelHashSet<uint>(2048, Allocator.Persistent),
                missingGlyphs = new NativeList<uint>(262144, Allocator.Persistent),
            };
            state.EntityManager.AddComponentData(state.SystemHandle, glyphAtlas);
            SystemAPI.TryGetSingletonRW<FontAtlasInfo>(out RefRW<FontAtlasInfo> fontAtlasInfo);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var glyphAtlas = SystemAPI.GetSingleton<FontAtlasInfo>();
            var findMissingGlyphsJob = new FindMissingGlyphsJob
            {
                glyphAtlas = glyphAtlas.glyphAtlas,
                missingGlyphs = glyphAtlas.missingGlyphs.AsParallelWriter(),
            };
            state.Dependency= findMissingGlyphsJob.ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var glyphAtlas = SystemAPI.GetSingleton<FontAtlasInfo>();
            glyphAtlas.glyphAtlas.Dispose();
            glyphAtlas.missingGlyphs.Dispose();
        }
    }
}
