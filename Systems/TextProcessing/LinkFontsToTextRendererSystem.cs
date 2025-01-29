using TextMeshDOTS.TextProcessing;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace TextMeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(RegisterFontMaterialSystem))]
    partial struct LinkMaterialsToTextSystem : ISystem
    {
        EntityQuery fontEntitiesQ, textQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontEntitiesQ = SystemAPI.QueryBuilder()
                    .WithAll<FontAssetRef>()
                    .WithAll<AtlasData>()
                    .WithAll<MissingGlyphs>()
                    .WithAll<UsedGlyphs>()
                    .WithAll<UsedGlyphRects>()
                    .WithAll<FreeGlyphRects>()
                    .WithAll<NativeFontPointer>()
                    .WithAll<DynamicFontAsset>()
                    .WithAll<MaterialMeshInfo>()
                    .Build();
            textQuery = SystemAPI.QueryBuilder()
                    .WithAll<FontBlobReference>()
                    .WithAbsent<MaterialMeshInfo>()
                    .Build();
            state.RequireForUpdate(textQuery);
            state.RequireForUpdate(fontEntitiesQ);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var materialMeshInfoLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(true);
            var fontEntities = fontEntitiesQ.ToEntityArray(state.WorldUpdateAllocator);
            var fontEntitiesLookup = fontEntitiesQ.ToComponentDataArray<FontAssetRef>(state.WorldUpdateAllocator);
            int fontEntityID;
            foreach (var (fontBlobReference, textEntity) in SystemAPI.Query<FontBlobReference>()
                .WithAll<FontBlobReference>()
                .WithAbsent<MaterialMeshInfo>()
                .WithEntityAccess())
            {
                if ((fontEntityID = fontEntitiesLookup.IndexOf(fontBlobReference.value.Value.fontAssetRef)) != -1)
                {
                    var fontEntity= fontEntities[fontEntityID];
                    if (materialMeshInfoLookup.HasComponent(fontEntity))
                    {
                        ecb.AddComponent(textEntity, materialMeshInfoLookup[fontEntity]);
                    }
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        
        }
    }
}
