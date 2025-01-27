using Unity.Burst;
using UnityEngine;

namespace TextMeshDOTS.Rendering.Authoring
{
    [BurstCompile]
    public static class TextMaterialUtility
    {        
        public const string kResourcePath = "Assets/Resources";

        public const string ktextMeshDOTS_URP_material = "TextMeshDOTS-URP";
        public const string ktextMeshDOTS_URP_path = "Assets/Resources/TextMeshDOTS-URP.mat";

        public const string kUnlit_material = "COLRv1-URP";
        public const string kUnlitPath = "Assets/Resources/COLRv1-URP.mat";

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TextMeshDOTS/Generate Materials")]
        static void CreateMaterialAssets()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(kResourcePath))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");


            var textMeshDOTSShader = Shader.Find("TextMeshDOTS/TextMeshDOTS-URP");
            var textMeshDOTSMaterial = new Material(textMeshDOTSShader);
            textMeshDOTSMaterial.enableInstancing = true;
            SetupMaterialWithBlendMode(textMeshDOTSMaterial);
            UnityEditor.AssetDatabase.CreateAsset(textMeshDOTSMaterial, ktextMeshDOTS_URP_path);

            var urpUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            var urpUnlitMaterial = new Material(urpUnlitShader);
            UnityEditor.AssetDatabase.CreateAsset(urpUnlitMaterial, kUnlitPath);
        }
#endif
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

