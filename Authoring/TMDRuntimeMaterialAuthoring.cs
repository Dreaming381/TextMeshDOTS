#if UNITY_EDITOR
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextMeshDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/TMD Runtime Material")]
    public class TMDRuntimeMaterialAuthoring : MonoBehaviour
    {
        [Tooltip("Drop here the material you wou like to use for Text Renderer spawned at runtime")]
        public Material material;
    }

    class TMDRuntimeMaterialBaker : Baker<TMDRuntimeMaterialAuthoring>
    {
        public override void Bake(TMDRuntimeMaterialAuthoring authoring)
        {
            DependsOn(authoring.material);
            if (authoring.material ==null)
                return;

            string[] guids = AssetDatabase.FindAssets("TextBackendMesh t:mesh", null);
            if (guids.Length == 0 || guids[0] == null)
                return;

            var backEndMesh = AssetDatabase.LoadAssetByGUID(new GUID(guids[0]), typeof(Mesh)) as Mesh;

            var entity = GetEntity(TransformUsageFlags.None);

            var runtimeFontMaterial = new RuntimeFontMaterial
            {
                material = authoring.material,
                backendMesh = backEndMesh,
                batchMaterialID = BatchMaterialID.Null,
                batchMeshID = BatchMeshID.Null,                
            };            
            AddComponent(entity, runtimeFontMaterial);           
        } 
    }    
}
#endif