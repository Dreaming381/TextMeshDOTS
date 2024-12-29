using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Jobs;

namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(ExtractTextSegmentsSystem))]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    //[DisableAutoCreation]
    public partial struct ShapeSystem : ISystem
    {
        EntityQuery m_query, fontEntityQ;
        static readonly ProfilerMarker marker = new ProfilerMarker("harfbuzz");
        static readonly ProfilerMarker marker2 = new ProfilerMarker("buffer");

        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = SystemAPI.QueryBuilder()
                      .WithAllRW<GlyphOTF>()
                      .WithAll<CalliByte>()
                      .Build();            

            fontEntityQ = SystemAPI.QueryBuilder()
                              .WithAll<UsedGlyphs>()
                              .WithAll<MissingGlyphs>()
                              .WithAll<DynamicFontAssets>()
                              .Build();

            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByte>());
            //m_query.AddChangedVersionFilter(ComponentType.ReadWrite<FontMaterial>());
            state.RequireForUpdate<FontHashMap>();
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fontHashMap = SystemAPI.GetSingleton<FontHashMap>();
            if (fontHashMap.fontsDirty == true)
                return;
            var fontEntities = fontHashMap.fontEntities;

            var missingGlyphs = new NativeList<FontEntityGlyph>(65536, Allocator.TempJob);

            state.Dependency = new ShapeJob
            {
                marker = marker,
                marker2 = marker2,
                
                missingGlyphs = missingGlyphs.AsParallelWriter(),

                fontEntities = fontEntities,
                entitesHandle = SystemAPI.GetEntityTypeHandle(),
                additionalFontMaterialEntityHandle = SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                fontBlobReferenceLookup = SystemAPI.GetComponentLookup<FontBlobReference>(true),
                nativeFontPointerLookup = SystemAPI.GetComponentLookup<NativeFontPointer>(),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(false),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(true),
                glyphsInUseLookup = SystemAPI.GetBufferLookup<UsedGlyphs>(true),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            //}.Schedule(m_query, state.Dependency);
            }.ScheduleParallel(m_query, state.Dependency);

            state.Dependency = new SortMissingGlyphJob
            {
                missingGlyphs = missingGlyphs,
            }.Schedule(state.Dependency);

            state.Dependency = new CopyMissingGlyphsToFontEntitiesJob
            {
                newMissingGlyphs= missingGlyphs,
            }.ScheduleParallel(fontEntityQ, state.Dependency);
            missingGlyphs.Dispose(state.Dependency);
        }
    }
}

