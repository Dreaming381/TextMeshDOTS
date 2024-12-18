using TextMeshDOTS.Rendering.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;


namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(NativeFontManagerSystem))]
    [RequireMatchingQueriesForUpdate]
    partial class RegisterFontMaterialSystem : SystemBase
    {
        EntityQuery fontEntityQ;
        EntitiesGraphicsSystem hybridRenderer;
        Shader textMeshDOTSShader;
        Mesh mesh;
        protected override void OnCreate()
        {
            mesh = Resources.Load<Mesh>(TextBackendBakingUtility.kTextBackendMeshResource);
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            fontEntityQ = SystemAPI.QueryBuilder()                    
                    .WithAll<HBFontAssetRef>()
                    .WithAll<FontTextureReference>()
                    .WithAll<HBUsedGlyphs>()
                    .WithAll<HBMissingGlyphs>()
                    .WithAll<HBFontPointer>()
                    .WithAbsent<MaterialMeshInfo>()  
                    .WithAbsent<CreatedFromFontAsset>()
                    .Build();
            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontTextureReference>());
            textMeshDOTSShader = Shader.Find("TextMeshDOTS/TextMeshDOTS-URP");
            RequireForUpdate<FontHashMap>();
        }

        protected override void OnUpdate()
        {
            if (fontEntityQ.IsEmpty)
                return;
            

            var entities = fontEntityQ.ToEntityArray(Allocator.TempJob);            

            foreach (var entity in entities)
            {
                var hbFontAssetRef = EntityManager.GetComponentData<HBFontAssetRef>(entity);
                Debug.Log($"Load texture for {hbFontAssetRef.family} {hbFontAssetRef.subFamily}");
                //System.IO.File.WriteAllBytes("Assets\\Resources\\Materials\\SDFtest.png", fontTextureReference.texture.Value.EncodeToPNG());
                var material = new Material(textMeshDOTSShader);
                material.enableInstancing = true;
                SetupMaterialWithBlendMode(material);

                var fontTextureReference = EntityManager.GetComponentData<FontTextureReference>(entity);
                var mainTexture = fontTextureReference.texture.Value;
                mainTexture.Apply();

                material.mainTexture = fontTextureReference.texture;
                fontTextureReference.material = material;
                var brgMaterialID = hybridRenderer.RegisterMaterial(material);
                var brgMeshID = hybridRenderer.RegisterMesh(mesh);

                EntityManager.AddComponentData(entity, new MaterialMeshInfo { MaterialID = brgMaterialID, MeshID= brgMeshID });
                EntityManager.SetComponentData(entity, fontTextureReference);
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