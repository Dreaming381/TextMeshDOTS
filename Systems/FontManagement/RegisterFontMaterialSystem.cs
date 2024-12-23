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
        EntityQuery fontEntityQ;
        EntitiesGraphicsSystem hybridRenderer;
        Shader textMeshDOTSShader;
        Mesh backendMesh;
        BatchMeshID brgBackendMeshID;
        protected override void OnCreate()
        {
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            backendMesh = Resources.Load<Mesh>(TextBackendBakingUtility.kTextBackendMeshResource);
            brgBackendMeshID = BatchMeshID.Null;

            fontEntityQ = SystemAPI.QueryBuilder()
                    .WithAll<FontBlobReference>()
                    .WithAll<AtlasData>()
                    .WithAll<DynamicFontAssets>()
                    .WithAll<UsedGlyphs>()
                    .WithAll<MissingGlyphs>()
                    .WithAll<NativeFontPointer>()
                    .WithAbsent<MaterialMeshInfo>()  
                    .Build();
            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontTextureReference>());
            textMeshDOTSShader = Shader.Find("TextMeshDOTS/TextMeshDOTS-URP");
            RequireForUpdate<FontHashMap>();
        }

        protected override void OnUpdate()
        {
            if (fontEntityQ.IsEmpty)
                return;

            if(brgBackendMeshID == BatchMeshID.Null)
                brgBackendMeshID = hybridRenderer.RegisterMesh(backendMesh);

            var entities = fontEntityQ.ToEntityArray(Allocator.TempJob);            

            foreach (var entity in entities)
            {
                var fontBlobRef = EntityManager.GetComponentData<FontBlobReference>(entity);
                //Debug.Log($"Load texture for font {fontBlobRef.value.Value.fontFamily} {fontBlobRef.value.Value.fontSubFamily}");
                //System.IO.File.WriteAllBytes("Assets\\Resources\\Materials\\SDFtest.png", fontTextureReference.texture.Value.EncodeToPNG());

                var material = new Material(textMeshDOTSShader);
                material.enableInstancing = true;
                SetupMaterialWithBlendMode(material);

                var dynamicFontAssets = EntityManager.GetComponentData<DynamicFontAssets>(entity);
                var mainTexture = dynamicFontAssets.texture.Value;
                mainTexture.Apply();

                material.mainTexture = dynamicFontAssets.texture;
                dynamicFontAssets.fontMaterialID = hybridRenderer.RegisterMaterial(material);                

                EntityManager.AddComponentData(entity, new MaterialMeshInfo { MaterialID = dynamicFontAssets.fontMaterialID, MeshID= brgBackendMeshID });
                EntityManager.SetComponentData(entity, dynamicFontAssets);
            }
            var fontHashMap = SystemAPI.GetSingletonRW<FontHashMap>();
            fontHashMap.ValueRW.fontsDirty = false;
            entities.Dispose();
            //this.Enabled = false;
        }
        public static void SetupMaterialWithBlendMode(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}