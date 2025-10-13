using Unity.Burst;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextMeshDOTS.Rendering.Authoring
{
    [BurstCompile]
    public static class TextMaterialUtility
    {        
        private const string kResourcePath = "Assets/Resources";
        //private const string kUnified_URP_Shader = "TextMeshDOTS/TMD_Simple_Unlit";
        //private const string kUnified_URP_MaterialPath = "Assets/Resources/Unified-URP.mat";

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TextMeshDOTS/Generate Materials")]
        static void CreateMaterialAssets()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(kResourcePath))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            Shader shader;
            Material material;

            shader = Shader.Find("TextMeshDOTS/TMD_Color_0_Outline_Lit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Color_0_Outline_Lit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Color_1_Outline_Lit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Color_1_Outline_Lit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Color_3_Outline_Lit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Color_3_Outline_Lit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Color_0_Outline_Unlit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Color_0_Outline_Unlit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Color_1_Outline_Unlit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Color_1_Outline_Unlit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Color_3_Outline_Unlit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Color_3_Outline_Unlit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Texture_0_Outline_Lit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Texture_0_Outline_Lit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Texture_1_Outline_Lit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Texture_1_Outline_Lit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Texture_3_Outline_Lit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Texture_3_Outline_Lit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Texture_0_Outline_Unlit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Texture_0_Outline_Unlit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Texture_1_Outline_Unlit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Texture_1_Outline_Unlit.mat");

            shader = Shader.Find("TextMeshDOTS/TMD_Texture_3_Outline_Unlit");
            material = new Material(shader);
            material.enableInstancing = true;
            SetupUnifiedMaterialWithBlendMode(material);
            UnityEditor.AssetDatabase.CreateAsset(material, $"{kResourcePath}/TMD_Texture_3_Outline_Unlit.mat");
        }
#endif

        public static void SetupUnifiedMaterialWithBlendMode(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Back);           
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}

