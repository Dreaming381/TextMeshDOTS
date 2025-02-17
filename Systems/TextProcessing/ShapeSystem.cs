using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Jobs;
using Unity.Collections;


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

        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontstateQ = SystemAPI.QueryBuilder()
                .WithAll<FontState>()
                .WithNone<FontsDirtyTag>()
                .Build();

            textRendererQ = SystemAPI.QueryBuilder()
                .WithAllRW<XMLTag>()
                .WithAllRW<GlyphOTF>()
                .WithAllRW<CalliByte>()                                
                .WithAll<CalliByteRaw>()                
                .WithAll<TextBaseConfiguration>()
                .WithAll<FontBlobReference>()
                .Build();            

            fontEntitiesQ = SystemAPI.QueryBuilder()
                .WithAll<FontAssetRef>()
                .WithAll<UsedGlyphs>()
                .WithAll<MissingGlyphs>()
                .WithAll<DynamicFontAsset>()
                .Build();

            textRendererQ.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByteRaw>());
            textRendererQ.AddChangedVersionFilter(ComponentType.ReadWrite<TextBaseConfiguration>());

            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
            state.RequireForUpdate(fontstateQ);
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (textRendererQ.IsEmpty)
                return;
            //Debug.Log("Shape system");

            var missingGlyphs = new NativeList<FontEntityGlyph>(65536, Allocator.TempJob);

            var fontEntities = fontEntitiesQ.ToEntityArray(state.WorldUpdateAllocator);
            var fontEntitiesLookup = fontEntitiesQ.ToComponentDataArray<FontAssetRef>(state.WorldUpdateAllocator);
            state.Dependency = new ExtractTagsJob
            {
                calliByteRawHandle = SystemAPI.GetBufferTypeHandle<CalliByteRaw>(true),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(false),
                xmlTagHandle = SystemAPI.GetBufferTypeHandle<XMLTag>(false),
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            }.ScheduleParallel(textRendererQ, state.Dependency);

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
                calliByteRawHandle = SystemAPI.GetBufferTypeHandle<CalliByteRaw>(true),
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

            missingGlyphs.Dispose(state.Dependency);
        }
    }
}