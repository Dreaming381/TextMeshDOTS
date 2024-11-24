using HarfBuzz;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TextMeshDOTS
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
                              .WithAll<GlyphInfo>()
                              .WithAll<FontAssetReference>()
                              .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<GlyphInfo>());

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
    public struct FontAtlasInfo : IComponentData
    {
        public NativeParallelHashSet<uint> glyphAtlas;
        public NativeList<uint> missingGlyphs;
    }

    [BurstCompile]
    public partial struct FindMissingGlyphsJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashSet<uint> glyphAtlas;
        public NativeList<uint>.ParallelWriter missingGlyphs;
        public void Execute(in DynamicBuffer<GlyphInfo> glyphInfoBuffer)
        {
            var glyphInfos = glyphInfoBuffer.AsNativeArray();
            for (int i = 0, ii = glyphInfos.Length; i< ii; i++) 
            {
                var glyphID = glyphInfos[i].codepoint;
                if (!glyphAtlas.Contains(glyphID))
                    missingGlyphs.AddNoResize(glyphID);
            }            
        }
    }
}
