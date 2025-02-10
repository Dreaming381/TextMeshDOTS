using TextmeshDOTS;
using TextMeshDOTS.Rendering.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;


namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(UpdateFontAtlasSystem))]
    [RequireMatchingQueriesForUpdate]
    partial class RegisterFontMaterialSystem : SystemBase
    {
        EntityQuery changedFontEntitiesQ, fontEntitiesQ, fontstateQ, textRendererQ;
        EntitiesGraphicsSystem hybridRenderer;

        Material textMeshDOTSMaterial;
        Material urpUnlitMaterial;
        Mesh backendMesh;
        BatchMeshID backendMeshID;
        
        protected override void OnCreate()
        {

            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            backendMesh = Resources.Load<Mesh>(TextBackendBakingUtility.kTextBackendMeshResource);
            backendMeshID = BatchMeshID.Null;
            textMeshDOTSMaterial = Resources.Load<Material>(TextMaterialUtility.kSDF_URP_Material);
            urpUnlitMaterial = Resources.Load<Material>(TextMaterialUtility.kCOLRv1_URP_Material);

            fontstateQ = SystemAPI.QueryBuilder()
                .WithAll<FontState>()
                .WithAll<FontsDirtyTag>()
                .Build();

            textRendererQ = SystemAPI.QueryBuilder()
                .WithAll<FontBlobReference>()
                .WithAll<MaterialMeshInfo>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();

            fontEntitiesQ = SystemAPI.QueryBuilder()
                .WithAll<FontAssetRef>()
                .WithAll<DynamicFontAsset>()
                .WithAll<NativeFontPointer>()
                .Build();

            changedFontEntitiesQ = SystemAPI.QueryBuilder()
                .WithAll<FontAssetRef>()
                .WithAll<DynamicFontAsset>()
                .WithAll<NativeFontPointer>()
                .Build();
            changedFontEntitiesQ.SetChangedVersionFilter(ComponentType.ReadWrite<FontAssetRef>());

            RequireForUpdate(fontstateQ);
            RequireForUpdate(changedFontEntitiesQ);
        }

        protected override void OnUpdate()
        {
            if (changedFontEntitiesQ.IsEmpty)
                return;

            //Debug.Log($"Register material, and link TextRender to fonts");
            if (backendMeshID == BatchMeshID.Null)
                backendMeshID = hybridRenderer.RegisterMesh(backendMesh);

            var fontStateEntity = fontstateQ.GetSingletonEntity();
            var changedFontEntities = changedFontEntitiesQ.ToEntityArray(WorldUpdateAllocator);
            var dynamicFontAssetLookup = SystemAPI.GetComponentLookup<DynamicFontAsset>(false);
            var fontAssetRefLookup = SystemAPI.GetComponentLookup<FontAssetRef>(false);

            foreach (var entity in changedFontEntities)
            {
                var dynamicFontAsset = dynamicFontAssetLookup[entity];
                var mainTexture = dynamicFontAsset.texture.Value;
                mainTexture.Apply();

                if (dynamicFontAsset.textureType == TextureType.SDF)
                {
                    var material = Object.Instantiate(textMeshDOTSMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.debugMaterial = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                    dynamicFontAsset.backendMeshID = backendMeshID;
                }
                else
                {
                    var material = Object.Instantiate(urpUnlitMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.debugMaterial = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                    dynamicFontAsset.backendMeshID = backendMeshID;
                }
                dynamicFontAssetLookup[entity] = dynamicFontAsset;
            }

            var allFontEntities = fontEntitiesQ.ToEntityArray(WorldUpdateAllocator);
            var fontEntityLookup = new NativeHashMap<FontAssetRef, Entity>(allFontEntities.Length, WorldUpdateAllocator);
            for(int i = 0, ii = allFontEntities.Length; i < ii; i++)
            {
                var entity = allFontEntities[i];
                var fontAssetRef = fontAssetRefLookup[entity];
                fontEntityLookup.Add(fontAssetRef, entity);
            }

            var updateMaterialMeshInfoJob = new EnableAndValidateMaterialMeshInfoJob
            {
                fontEntityLookup = fontEntityLookup,
                dynamicFontAssetLookup = dynamicFontAssetLookup,
            };
            Dependency = updateMaterialMeshInfoJob.ScheduleParallel(textRendererQ, Dependency);

            EntityManager.RemoveComponent<FontsDirtyTag>(fontStateEntity);
        }
    }
}