using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;
using HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TextMeshDOTS.TextProcessing
{
    //[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
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
                      //.WithAll<FontMaterial>()
                      .WithAll<FontEntity>()
                      .Build();            

            fontEntityQ = SystemAPI.QueryBuilder()
                              .WithAll<GlyphsInUse>()
                              .WithAll<MissingGlyphs>()
                              //.WithAll<DynamicFontBlobReference>()
                              .WithAll<FontTextureReference>()
                              .Build();

            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByte>());
            //m_query.AddChangedVersionFilter(ComponentType.ReadWrite<FontMaterial>());

            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var missingGlyphs = new NativeList<FontEntityGlyph>(65536, Allocator.TempJob);

            state.Dependency = new ShapeJob
            {
                marker = marker,
                marker2 = marker2,
                
                missingGlyphs = missingGlyphs.AsParallelWriter(),
                selectorHandle = SystemAPI.GetBufferTypeHandle<FontMaterialSelectorForGlyph>(false),
                fontEntityHandle = SystemAPI.GetBufferTypeHandle<FontEntity>(true),
                hbFontPointerLookup = SystemAPI.GetComponentLookup<HBFontPointer>(),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(false),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(true),
                glyphsInUseLookup = SystemAPI.GetBufferLookup<GlyphsInUse>(true),

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

