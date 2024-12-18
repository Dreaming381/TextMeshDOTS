using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace TextMeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    partial struct LinkMaterialsToTextSystem : ISystem
    {
        EntityQuery fontEntityQ, textQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontEntityQ = SystemAPI.QueryBuilder()
                    .WithAll<HBFontAssetRef>()
                    .WithAll<HBMissingGlyphs>()
                    .WithAll<HBUsedGlyphs>()
                    .WithAll<HBUsedGlyphRects>()
                    .WithAll<HBFreeGlyphRects>()
                    .WithAll<HBFontPointer>()
                    .WithAll<FontTextureReference>()
                    .WithAll<MaterialMeshInfo>()
                    .Build();
            textQuery = SystemAPI.QueryBuilder()
                    .WithAll<FontBlobReference>()
                    .WithAbsent<MaterialMeshInfo>()
                    .Build();
            state.RequireForUpdate(textQuery);
            state.RequireForUpdate(fontEntityQ);
            state.RequireForUpdate<FontHashMap>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var fontHashMap = SystemAPI.GetSingleton<FontHashMap>();
            var fontEntities = fontHashMap.fontEntities;
            var materialMeshInfoLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(true);

            foreach (var (fontBlobReference, textEntity) in SystemAPI.Query<FontBlobReference>()
                .WithAll<FontBlobReference>()
                .WithAbsent<MaterialMeshInfo>()
                .WithEntityAccess())
            {
                if (fontEntities.TryGetValue(fontBlobReference.fontBlob.Value.fontAssetRef, out var fontEntity))
                {
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
