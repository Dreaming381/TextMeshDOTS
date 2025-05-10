using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using System;

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

            var glyphTable = new GlyphTable
            {
                entries = new NativeList<GlyphTable.Entry>(1024, Allocator.Persistent),
                glyphHashToIdMap = new NativeHashMap<GlyphTable.Key, uint>(1024, Allocator.Persistent)
            };
            state.EntityManager.CreateSingleton(glyphTable);
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

            var chunkCount = textRendererQ.CalculateChunkCountWithoutFiltering();
            var missingGlyphStream = new NativeStream(chunkCount, state.WorldUpdateAllocator);
            var glyphTable = SystemAPI.GetSingletonRW<GlyphTable>().ValueRW;
            var fontTable = SystemAPI.GetSingleton<FontTable>();


            state.Dependency = new ShapeJob
            {
                marker = marker,
                marker2 = marker2,

                missingGlyphs = missingGlyphs.AsParallelWriter(),
                missingGlyphsStream = missingGlyphStream.AsWriter(),

                glyphTable = glyphTable,
                fontTable = fontTable,
                entitesHandle = SystemAPI.GetEntityTypeHandle(),
                additionalFontMaterialEntityHandle = SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                fontBlobReferenceLookup = SystemAPI.GetComponentLookup<FontBlobReference>(true),
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(true),
                glyphOTFHandle = SystemAPI.GetBufferTypeHandle<GlyphOTF>(false),
                selectorHandle = SystemAPI.GetBufferTypeHandle<FontMaterialSelectorForGlyph>(false),
                xmlTagHandle = SystemAPI.GetBufferTypeHandle<XMLTag>(true),
                glyphsInUseLookup = SystemAPI.GetBufferLookup<UsedGlyphs>(true),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            }.ScheduleParallel(textRendererQ, state.Dependency);

            var missingGlyphsToAdd = new NativeList<GlyphTable.Key>(state.WorldUpdateAllocator);
            state.Dependency = new AllocateNewGlyphsJob
            {
                fontTable = fontTable,
                glyphTable = glyphTable,
                missingGlyphsStream = missingGlyphStream.AsReader(),
                missingGlyphsToAdd = missingGlyphsToAdd
            }.Schedule(state.Dependency);

            state.Dependency = new PopulateNewGlyphsJob
            {
                fontTable = fontTable,
                glyphEntries = glyphTable.entries.AsDeferredJobArray(),
                missingGlyphs = missingGlyphsToAdd.AsDeferredJobArray()
            }.Schedule(missingGlyphsToAdd, 32, state.Dependency);

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
            state.CompleteDependency();
            SystemAPI.GetSingletonRW<GlyphTable>().ValueRW.TryDispose(default).Complete();
        }

        [BurstCompile]
        struct AllocateNewGlyphsJob : IJob
        {
            [ReadOnly] public FontTable fontTable;
            public GlyphTable glyphTable;
            public NativeStream.Reader missingGlyphsStream;
            public NativeList<GlyphTable.Key> missingGlyphsToAdd;

            public void Execute()
            {
                // Deduplicate
                var requestCount = missingGlyphsStream.Count();
                var uniqueMissingGlyphSet = new UnsafeHashSet<GlyphTable.Key>(requestCount, Allocator.Temp);
                for (int chunk = 0; chunk < missingGlyphsStream.ForEachCount; chunk++)
                {
                    var elementsInChunk = missingGlyphsStream.BeginForEachIndex(chunk);
                    for (int i = 0; i < elementsInChunk; i++)
                    {
                        var key = missingGlyphsStream.Read<GlyphTable.Key>();
                        uniqueMissingGlyphSet.Add(key);
                    }
                }

                missingGlyphsToAdd.Capacity = uniqueMissingGlyphSet.Count;
                uint nextIndex = (uint)glyphTable.glyphHashToIdMap.Count;
                foreach (var key in uniqueMissingGlyphSet)
                {
                    missingGlyphsToAdd.AddNoResize(key);
                    var nextId = nextIndex;
                    Bits.SetBits(ref nextId, 30, 2, (uint)key.format);
                    glyphTable.glyphHashToIdMap.Add(key, nextId);
                    nextIndex++;
                }
                glyphTable.entries.AddReplicate(default, missingGlyphsToAdd.Length);
            }
        }

        [BurstCompile]
        struct PopulateNewGlyphsJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<GlyphTable.Key> missingGlyphs;
            [ReadOnly] public FontTable fontTable;
            [NativeDisableParallelForRestriction] public NativeArray<GlyphTable.Entry> glyphEntries;

            [NativeSetThreadIndex]
            int threadIndex;

            GlyphTable.Key lastKey;
            [NativeDisableUnsafePtrRestriction] IntPtr lastFontPtr;
            bool initialized;

            public void Execute(int i)
            {
                var missingGlyph = missingGlyphs[i];
                var fontPtr = lastFontPtr;

                if (!initialized || RequiresFontSetup(lastKey, missingGlyph))
                {
                    fontPtr = fontTable.GetOrCreateFont(missingGlyph.faceIndex, threadIndex);
                    var samplingSize = missingGlyph.textureSize.GetSamplingSize();
                    Harfbuzz.hb_font_set_scale(fontPtr, samplingSize, samplingSize);
                    initialized = true;
                    lastFontPtr = fontPtr;
                }

                Harfbuzz.hb_font_get_glyph_extents(fontPtr, missingGlyph.glyphIndex, out var extents);

                var newEntry = new GlyphTable.Entry
                {
                    key = missingGlyph,
                    refCount = 0,
                    x = -1,
                    y = -1,
                    z = -1,
                    width = (short)extents.width,
                    height = (short)(-extents.height),  // For legacy reasons, Harfbuzz returns height as negative.
                    xBearing = (short)extents.x_bearing,
                    yBearing = (short)extents.y_bearing  // Harfbuzz is y-up
                };
                var baseIndex = glyphEntries.Length - missingGlyphs.Length;
                glyphEntries[baseIndex + i] = newEntry;
            }

            bool RequiresFontSetup(GlyphTable.Key lastKey, GlyphTable.Key thisKey)
            {
                var a = lastKey.packed & 0xffffffffffff0000;
                var b = thisKey.packed & 0xffffffffffff0000;
                return a != b;
            }
        }
    }
}