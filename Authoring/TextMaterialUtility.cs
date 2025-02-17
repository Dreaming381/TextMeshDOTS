using Unity.Burst;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextMeshDOTS.Rendering.Authoring
{
    [BurstCompile]
    public static class TextMaterialUtility
    {        
        public const string kResourcePath = "Assets/Resources";

        private const string kSDF_HDRP_Shader = "TextMeshDOTS/SDF-HDRP";
        public const string kSDF_HDRP_Material = "TextMeshDOTS-HDRP";
        public const string kSDF_HDRP_MaterialPath = "Assets/Resources/SDF-HDRP.mat";

        private const string kSDF_URP_Shader = "TextMeshDOTS/SDF-URP";
        public const string kSDF_URP_Material = "SDF-URP";
        public const string kSDF_URP_MaterialPath = "Assets/Resources/SDF-URP.mat";

        private const string kCOLRv1_HDRP_Shader = "TextMeshDOTS/COLRv1-HDRP";
        public const string kCOLRv1_HDRP_Material = "COLRv1-HDRP";
        public const string kCOLORv1_HDRP_MaterialPath = "Assets/Resources/COLRv1-HDRP.mat";

        private const string kCOLRv1_URP_Shader = "TextMeshDOTS/COLRv1-URP";
        public const string kCOLRv1_URP_Material = "COLRv1-URP";
        public const string kCOLORv1_URP_MaterialPath = "Assets/Resources/COLRv1-URP.mat";

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TextMeshDOTS/Generate Materials")]
        static void CreateMaterialAssets()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(kResourcePath))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            var shader = Shader.Find(kSDF_HDRP_Shader);
            var material = new Material(shader);
            material.enableInstancing = true;
            SetupMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, kSDF_HDRP_MaterialPath);

            shader = Shader.Find(kSDF_URP_Shader);
            material = new Material(shader);
            material.enableInstancing = true;            
            SetupMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, kSDF_URP_MaterialPath);

            shader = Shader.Find(kCOLRv1_HDRP_Shader);            
            material = new Material(shader);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.enableInstancing = true;            
            UnityEditor.AssetDatabase.CreateAsset(material, kCOLORv1_HDRP_MaterialPath);

            shader = Shader.Find(kCOLRv1_URP_Shader);
            material = new Material(shader);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.enableInstancing = true;
            UnityEditor.AssetDatabase.CreateAsset(material, kCOLORv1_URP_MaterialPath);
        }
#endif
        public static void SetupMaterialWithBlendMode(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Back);
            // _IsoPerimeter = Outline Width. Increase x a bit to avoid that thin lines dissappear.
            // Best compromize in combination with current automatic calculation of SPREAD in SDF generation. 
            material.SetVector("_IsoPerimeter", new Vector4(0.15f,0,0,0));
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}

