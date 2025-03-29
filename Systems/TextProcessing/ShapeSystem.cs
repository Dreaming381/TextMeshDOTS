using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;


namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    //[DisableAutoCreation]
    public partial struct ShapeSystem : ISystem
    {
        EntityQuery textRendererQ, fontEntitiesQ, fontstateQ;
        static readonly ProfilerMarker marker = new ProfilerMarker("hb_shape");
        static readonly ProfilerMarker marker2 = new ProfilerMarker("buffer");
        NativeList<FontEntityGlyph> missingGlyphs;

        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            missingGlyphs = new NativeList<FontEntityGlyph>(65536, Allocator.Persistent);

            fontstateQ = SystemAPI.QueryBuilder()
                .WithAll<FontState>()
                .WithNone<FontsDirtyTag>()
                .Build();

            textRendererQ = SystemAPI.QueryBuilder()
                .WithAllRW<XMLTag>()
                .WithAllRW<GlyphOTF>()
                .WithAllRW<CalliByte>()           
                .WithAll<TextBaseConfiguration>()
                .WithAll<FontBlobReference>()
                .Build();            

            fontEntitiesQ = SystemAPI.QueryBuilder()
                .WithAll<FontAssetRef>()
                .WithAll<UsedGlyphs>()
                .WithAll<MissingGlyphs>()
                .WithAll<DynamicFontAsset>()
                .Build();

            //do not filter on query in release version, rather determine in jobs if chunk needs to be processed or not
            //textRendererQ.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByte>()); 
            //textRendererQ.AddChangedVersionFilter(ComponentType.ReadWrite<TextBaseConfiguration>());

            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
            state.RequireForUpdate(fontstateQ);
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //if (textRendererQ.IsEmpty)
            //    return;
            //Debug.Log("Shape system");
            
            state.Dependency = new ExtractTagsJob
            {
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                xmlTagHandle = SystemAPI.GetBufferTypeHandle<XMLTag>(false),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            }.ScheduleParallel(textRendererQ, state.Dependency);

            var fontEntities = fontEntitiesQ.ToEntityArray(state.WorldUpdateAllocator);
            var fontEntitiesLookup = fontEntitiesQ.ToComponentDataArray<FontAssetRef>(state.WorldUpdateAllocator);
            state.Dependency = new ShapeJob
            {
                marker = marker,
                marker2 = marker2,

                missingGlyphs = missingGlyphs.AsParallelWriter(),

                fontEntities = fontEntities,
                fontAssetRefs = fontEntitiesLookup,
                entitesHandle = SystemAPI.GetEntityTypeHandle(),
                additionalFontMaterialEntityHandle = SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                fontBlobReferenceLookup = SystemAPI.GetComponentLookup<FontBlobReference>(true),
                nativeFontPointerLookup = SystemAPI.GetComponentLookup<NativeFontPointer>(),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(false),
                selectorHandle = SystemAPI.GetBufferTypeHandle<FontMaterialSelectorForGlyph>(false),
                xmlTagHandle = SystemAPI.GetBufferTypeHandle<XMLTag>(true),
                glyphsInUseLookup = SystemAPI.GetBufferLookup<UsedGlyphs>(true),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            }.ScheduleParallel(textRendererQ, state.Dependency);

            state.Dependency = new SortMissingGlyphJob
            {
                missingGlyphs = missingGlyphs,
            }.Schedule(state.Dependency);

            state.Dependency = new CopyMissingGlyphsToFontEntitiesJob
            {
                newMissingGlyphs = missingGlyphs,
            }.ScheduleParallel(fontEntitiesQ, state.Dependency);

            state.Dependency = new ClearMissingGlyphJob
            {
                missingGlyphs = missingGlyphs,
            }.Schedule(state.Dependency);
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (missingGlyphs.IsCreated) missingGlyphs.Dispose();
        }
    }
}