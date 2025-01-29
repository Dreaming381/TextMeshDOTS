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
        EntityQuery fontEntitiesQ, fontstateQ;
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
            textMeshDOTSMaterial = Resources.Load<Material>(TextMaterialUtility.ktextMeshDOTS_URP_material);
            urpUnlitMaterial = Resources.Load<Material>(TextMaterialUtility.kUnlit_material);

            fontstateQ = SystemAPI.QueryBuilder()
                      .WithAll<FontState>()
                      .WithAll<FontsDirtyTag>()
                      .Build();
            fontEntitiesQ = SystemAPI.QueryBuilder()
                    .WithAll<FontAssetRef>()
                    .WithAll<AtlasData>()
                    .WithAll<DynamicFontAsset>()
                    .WithAll<UsedGlyphs>()
                    .WithAll<MissingGlyphs>()
                    .WithAll<NativeFontPointer>()
                    .WithAbsent<MaterialMeshInfo>()  
                    .Build();
            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontTextureReference>());
            
            RequireForUpdate(fontstateQ);
        }

        protected override void OnUpdate()
        {
            if (fontEntitiesQ.IsEmpty)
                return;

            if (backendMeshID == BatchMeshID.Null)
                backendMeshID = hybridRenderer.RegisterMesh(backendMesh);

            var fontStateEntity = fontstateQ.GetSingletonEntity();
            var entities = fontEntitiesQ.ToEntityArray(WorldUpdateAllocator);

            foreach (var entity in entities)
            {
                var dynamicFontAsset = EntityManager.GetComponentData<DynamicFontAsset>(entity);
                var mainTexture = dynamicFontAsset.texture.Value;
                mainTexture.Apply();

                if (dynamicFontAsset.textureType == TextureType.SDF)
                {
                    var material = Object.Instantiate(textMeshDOTSMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.debugMaterial = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                }
                else
                {
                    var material = Object.Instantiate(urpUnlitMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.debugMaterial = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                }

                EntityManager.AddComponentData(entity, new MaterialMeshInfo { MaterialID = dynamicFontAsset.fontMaterialID, MeshID= backendMeshID });
                EntityManager.SetComponentData(entity, dynamicFontAsset);
            }
            
            EntityManager.RemoveComponent<FontsDirtyTag>(fontStateEntity);
            //EntityManager.AddComponent<RebuildTextRenderTag>(fontStateEntity);

            //this.Enabled = false;
        }
    }
}