using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace TextMeshDOTS
{
    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    public partial struct EnableAndValidateMaterialMeshInfoJob : IJobEntity
    {
        [ReadOnly] public NativeHashMap<int, Entity> faceIndexToFontEntityMap;
        [ReadOnly] public NativeHashMap<FontAssetRef, int> fontAssetRefToFaceIndexMap;
        [ReadOnly] public ComponentLookup<DynamicFontAsset> dynamicFontAssetLookup;
        public void Execute(in FontBlobReference fontBlobReference, EnabledRefRW<MaterialMeshInfo> textRendererState, ref MaterialMeshInfo textRendererMaterialMeshInfo)
        {
            var fontAssetRef = fontBlobReference.value;
            if (fontAssetRefToFaceIndexMap.TryGetValue(fontAssetRef, out var id) && faceIndexToFontEntityMap.TryGetValue(id, out var fontEntity))
            {
                var dynamicFontAsset = dynamicFontAssetLookup[fontEntity];
                if (textRendererState.ValueRO == false)  //if rendering is not enabled, then enable it
                {
                    textRendererState.ValueRW = true;
                    textRendererMaterialMeshInfo = new MaterialMeshInfo { MaterialID = dynamicFontAsset.fontMaterialID, MeshID = dynamicFontAsset.backendMeshID };
                }
                else //if rendering is enabled, then validate correct fontMaterialID 
                {
                    if (textRendererMaterialMeshInfo.MaterialID != dynamicFontAsset.fontMaterialID)
                        textRendererMaterialMeshInfo.MaterialID = dynamicFontAsset.fontMaterialID;
                }
            }
            //else
            //    Debug.Log($"Unexpected: TextRender requieres FontMaterial that is not yet registered with hybridRenderer");
        }
    }
}
