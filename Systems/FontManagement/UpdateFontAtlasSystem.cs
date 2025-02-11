using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Profiling;
using Unity.Jobs;
using TextMeshDOTS.HarfBuzz;
using Unity.Burst;
using Unity.Mathematics;


namespace TextMeshDOTS.TextProcessing
{
    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(ShapeSystem))]
    partial struct UpdateFontAtlasSystem : ISystem
    {
        EntityQuery fontEntityQ;

        static readonly ProfilerMarker marker = new ProfilerMarker("COLR");
        static readonly ProfilerMarker marker2 = new ProfilerMarker("SDF");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontEntityQ = SystemAPI.QueryBuilder()
                .WithAll<AtlasData>()
                .WithAll<MissingGlyphs>()
                .WithAll<UsedGlyphs>()
                .WithAll<UsedGlyphRects>()
                .WithAll<FreeGlyphRects>()
                .WithAll<NativeFontPointer>()
                .WithAll<DynamicFontAsset>()
                .Build();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if(fontEntityQ.IsEmpty) 
                return;
            
            var fontsRequiringUpdate = new NativeList<Entity>(16, Allocator.TempJob);
            foreach (var (fontAssetRef, fontAssetMetadata, missingGlyphs, entity) in SystemAPI.Query<FontAssetRef, FontAssetMetadata, DynamicBuffer<MissingGlyphs>>()
                .WithAll<FontAssetRef>()
                .WithAll<FontAssetMetadata>()
                .WithAll<AtlasData>()
                .WithAll<MissingGlyphs>()
                .WithEntityAccess())
            {
                if (missingGlyphs.Length > 0)
                {
                    fontsRequiringUpdate.Add(entity);
                    //Debug.Log($"Request to add {missingGlyphs.Length} glyphs to texture of {fontAssetMetadata.family} {fontAssetMetadata.subfamily}");
                }
            }
            if(fontsRequiringUpdate.IsEmpty)
            {
                fontsRequiringUpdate.Dispose();
                return;
            }

            state.Dependency.Complete();
            var glyphsToPlace = new NativeList<GlyphBlob>(1024, Allocator.TempJob);
            var placedGlyphs = new NativeList<GlyphBlob> (1024, Allocator.TempJob);
            var fontAssetMetadataLookup = SystemAPI.GetComponentLookup<FontAssetMetadata>(true);
            var atlasDataLookup = SystemAPI.GetComponentLookup<AtlasData>(true);
            var missingGlyphsLookup = SystemAPI.GetBufferLookup<MissingGlyphs>(false);
            var usedGlyphsLookup = SystemAPI.GetBufferLookup<UsedGlyphs>(false);
            var usedGlyphRectsLookup = SystemAPI.GetBufferLookup<UsedGlyphRects>(false);
            var freeGlyphRectsLookup = SystemAPI.GetBufferLookup<FreeGlyphRects>(false);
            var nativeFontPointerLookup = SystemAPI.GetComponentLookup<NativeFontPointer>(true);
            var dynamicFontAssetsLookup = SystemAPI.GetComponentLookup<DynamicFontAsset>(false);

            for (int i = 0, ii = fontsRequiringUpdate.Length; i < ii; i++)
            {
                var fontEntity = fontsRequiringUpdate[i];

                ////for unknown reasons, parallel processing backfires: each thread takes as long as a single job thread) 
                //var missingGlyphsBuffer = missingGlyphsLookup[fontEntity].Reinterpret<uint>();
                //var glyphExtents = new NativeList<GlyphExtents>(missingGlyphsBuffer.Length, state.WorldUpdateAllocator);
                //var getGlyphExtentsJob = new GetGlyphExtentsJob()
                //{
                //    glyphExtents = glyphExtents.AsParallelWriter(),
                //    fontEntity = fontEntity,
                //    nativeFontPointerLookup = nativeFontPointerLookup,
                //    missingGlyphsBuffer = missingGlyphsBuffer,
                //};
                //state.Dependency = getGlyphExtentsJob.Schedule(missingGlyphsBuffer.Length, 1, state.Dependency);

                var getGlyphRectsJob = new GetGlyphRectsJob()
                {
                    placedGlyphs = placedGlyphs,

                    fontEntity = fontEntity,
                    fontAssetMetadataLookup = fontAssetMetadataLookup,
                    atlasDataLookup = atlasDataLookup,
                    nativeFontPointerLookup = nativeFontPointerLookup,

                    missingGlyphsBuffer = missingGlyphsLookup,
                    usedGlyphsBuffer = usedGlyphsLookup,
                    usedGlyphRectsBuffer = usedGlyphRectsLookup,
                    freeGlyphRectsBuffer = freeGlyphRectsLookup,
                };
                state.Dependency = getGlyphRectsJob.Schedule(state.Dependency);

                var dynamicFontAsset = dynamicFontAssetsLookup[fontEntity];
                if (dynamicFontAsset.textureType == TextureType.SDF)
                {
                    var updateAtlasTextureJob = new UpdateSDFAtlasTextureJob()
                    {
                        //this managed call to texture object is reason why we cannot BURST compile the update method of this system
                        textureData = dynamicFontAsset.texture.Value.GetRawTextureData<byte>(), 

                        fontEntity = fontEntity,
                        placedGlyphs = placedGlyphs,
                        atlasDataLookup = atlasDataLookup,
                        nativeFontPointerLookup = nativeFontPointerLookup,
                        usedGlyphsBuffer = usedGlyphsLookup,
                        usedGlyphRectsBuffer = usedGlyphRectsLookup,
                        marker = marker2,
                    };
                    state.Dependency = updateAtlasTextureJob.Schedule(placedGlyphs, 1, state.Dependency);
                }
                else if (dynamicFontAsset.textureType == TextureType.ARGB)
                {
                    var updateAtlasTextureJob = new UpdateBitmapAtlasTextureJob()
                    {
                        //this managed call to texture object is reason why we cannot BURST compile the update method of this system
                        textureData = dynamicFontAsset.texture.Value.GetRawTextureData<ColorARGB>(),

                        fontEntity = fontEntity,
                        placedGlyphs = placedGlyphs,
                        atlasDataLookup = atlasDataLookup,
                        nativeFontPointerLookup = nativeFontPointerLookup,
                        usedGlyphsBuffer = usedGlyphsLookup,
                        usedGlyphRectsBuffer = usedGlyphRectsLookup,
                        marker = marker,
                    };
                    state.Dependency = updateAtlasTextureJob.Schedule(placedGlyphs, 1, state.Dependency);
                }

                var updateNativeFontJob = new UpdateNativeFontJob()
                {
                    dynamicFontAssetLookup = dynamicFontAssetsLookup,

                    fontEntity = fontEntity,
                    atlasDataLookup = atlasDataLookup,
                    nativeFontPointerLookup = nativeFontPointerLookup,
                    placedGlyphs = placedGlyphs,
                };
                state.Dependency = updateNativeFontJob.Schedule(state.Dependency);

                state.Dependency.Complete(); //To-Do: remove sync point. 

                dynamicFontAsset.texture.Value.Apply();
                placedGlyphs.Clear();
            }

            glyphsToPlace.Dispose(state.Dependency);
            placedGlyphs.Dispose(state.Dependency);            
            fontsRequiringUpdate.Dispose(state.Dependency);
        }
    }
}