using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;


namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]    
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    //[UpdateAfter(typeof(UpdateFontAtlasSystem))]
    [RequireMatchingQueriesForUpdate]
    partial class RegisterFontMaterialSystem : SystemBase
    {
        EntityQuery changedFontEntitiesQ, fontstateQ, textRendererQ;
        EntitiesGraphicsSystem hybridRenderer;

        Material sdfMaterial;
        Material colrMaterial;
        Mesh backendMesh;
        BatchMeshID backendMeshID;
        
        protected override void OnCreate()
        {
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            backendMesh = Resources.Load<Mesh>(TextBackendBakingUtility.kTextBackendMeshResource);
            backendMeshID = BatchMeshID.Null;
            var srpType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
            if (srpType.Contains("HDRenderPipelineAsset"))
            {
                //Debug.Log("High Definition Render Pipeline (HDRP) is being used.");
                sdfMaterial = Resources.Load<Material>(TextMaterialUtility.kSDF_HDRP_Material);
                colrMaterial = Resources.Load<Material>(TextMaterialUtility.kCOLRv1_HDRP_Material);
            }
            else if (srpType.Contains("UniversalRenderPipelineAsset") || srpType.Contains("LightweightRenderPipelineAsset"))
            {
                //Debug.Log("Universal Render Pipeline (URP) is being used.");
                sdfMaterial = Resources.Load<Material>(TextMaterialUtility.kSDF_URP_Material);
                colrMaterial = Resources.Load<Material>(TextMaterialUtility.kCOLRv1_URP_Material);
            }
            else
                Debug.LogError("TextMeshDOTS does not work with the Built-in (Legacy) Render Pipeline");            

            fontstateQ = SystemAPI.QueryBuilder()
                .WithAll<FontState>()
                .WithAll<FontsDirtyTag>()
                .Build();

            textRendererQ = SystemAPI.QueryBuilder()
                .WithAll<FontBlobReference>()
                .WithAll<MaterialMeshInfo>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();

            changedFontEntitiesQ = SystemAPI.QueryBuilder()
                .WithAll<FontAssetRef>()
                .WithAll<DynamicFontAsset>()
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
                    var material = Object.Instantiate(sdfMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.debugMaterial = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                    dynamicFontAsset.backendMeshID = backendMeshID;
                }
                else
                {
                    var material = Object.Instantiate(colrMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.debugMaterial = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                    dynamicFontAsset.backendMeshID = backendMeshID;
                }
                dynamicFontAssetLookup[entity] = dynamicFontAsset;
            }

            var fontTable = SystemAPI.GetSingleton<FontTable>();
            CompleteDependency(); //needed?
            var updateMaterialMeshInfoJob = new EnableAndValidateMaterialMeshInfoJob
            {
                fontAssetRefToFaceIndexMap = fontTable.fontAssetRefToFaceIndexMap,
                faceIndexToFontEntityMap = fontTable.faceIndexToFontEntityMap,
                dynamicFontAssetLookup = dynamicFontAssetLookup,
            };
            Dependency = updateMaterialMeshInfoJob.ScheduleParallel(textRendererQ, Dependency);

            EntityManager.RemoveComponent<FontsDirtyTag>(fontStateEntity);
        }
    }
}