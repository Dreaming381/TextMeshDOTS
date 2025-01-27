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
        EntityQuery fontEntityQ, fontsQ;
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

            fontsQ = SystemAPI.QueryBuilder()
                      .WithAll<FontState>()
                      .WithAll<FontsDirtyTag>()
                      .Build();
            fontEntityQ = SystemAPI.QueryBuilder()
                    .WithAll<FontBlobReference>()
                    .WithAll<AtlasData>()
                    .WithAll<DynamicFontAsset>()
                    .WithAll<UsedGlyphs>()
                    .WithAll<MissingGlyphs>()
                    .WithAll<NativeFontPointer>()
                    .WithAbsent<MaterialMeshInfo>()  
                    .Build();
            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontTextureReference>());
            
            RequireForUpdate(fontsQ);
            SystemAPI.TryGetSingletonRW<FontHashMap>(out _);//still needed to create system dependency?
        }

        protected override void OnUpdate()
        {
            if (fontEntityQ.IsEmpty)
                return;

            if (backendMeshID == BatchMeshID.Null)
                backendMeshID = hybridRenderer.RegisterMesh(backendMesh);

            var fontStateEntity = fontsQ.GetSingletonEntity();
            var entities = fontEntityQ.ToEntityArray(Allocator.TempJob);            

            foreach (var entity in entities)
            {
                var fontBlobRef = EntityManager.GetComponentData<FontBlobReference>(entity);
                //Debug.Log($"Load texture for font {fontBlobRef.value.Value.fontFamily} {fontBlobRef.value.Value.fontSubFamily}");
                //System.IO.File.WriteAllBytes("Assets\\Resources\\Materials\\SDFtest.png", fontTextureReference.texture.Value.EncodeToPNG());
                
                var dynamicFontAsset = EntityManager.GetComponentData<DynamicFontAsset>(entity);
                var mainTexture = dynamicFontAsset.texture.Value;
                mainTexture.Apply();

                if (dynamicFontAsset.textureType == TextureType.SDF)
                {
                    var material = Object.Instantiate(textMeshDOTSMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.materialDebug = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                }
                else
                {
                    var material = Object.Instantiate(urpUnlitMaterial);
                    material.mainTexture = dynamicFontAsset.texture;
                    dynamicFontAsset.materialDebug = material;
                    dynamicFontAsset.fontMaterialID = hybridRenderer.RegisterMaterial(material);
                }

                EntityManager.AddComponentData(entity, new MaterialMeshInfo { MaterialID = dynamicFontAsset.fontMaterialID, MeshID= backendMeshID });
                EntityManager.SetComponentData(entity, dynamicFontAsset);
            }
            
            EntityManager.RemoveComponent<FontsDirtyTag>(fontStateEntity);
            //EntityManager.AddComponent<RebuildTextRenderTag>(fontStateEntity);

            entities.Dispose();
            //this.Enabled = false;
        }
    }
}