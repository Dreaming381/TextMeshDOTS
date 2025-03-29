using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace TextMeshDOTS.TextProcessing
{

    partial class FontCleanupSystem : SystemBase
    {
        EntitiesGraphicsSystem hybridRenderer;
        protected override void OnCreate()
        {
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        }
        protected override void OnUpdate()
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);

            foreach (var (nativeFontPointer, entity) in SystemAPI.Query<NativeFontPointer>()
                .WithAll<NativeFontPointer>()
                .WithNone<UsedGlyphs>()
                .WithNone<MissingGlyphs>()          
                .WithEntityAccess())
            {                
                //Debug.Log($"Destroy Harfbuzz font pointer");
                nativeFontPointer.blob.Dispose();
                nativeFontPointer.face.Dispose();
                nativeFontPointer.font.Dispose();
                ecb.RemoveComponent<NativeFontPointer>(entity);
            }

            foreach (var (dynamicFontAsset, entity) in SystemAPI.Query<DynamicFontAsset>()
                .WithAll<DynamicFontAsset>()
                .WithNone<UsedGlyphs>()
                .WithNone<MissingGlyphs>()         
                .WithEntityAccess())
            {
                //Debug.Log($"Destroy font material");
                if(dynamicFontAsset.blob.IsCreated) dynamicFontAsset.blob.Dispose();
                var fontMaterial = hybridRenderer.GetMaterial(dynamicFontAsset.fontMaterialID);
                hybridRenderer.UnregisterMaterial(dynamicFontAsset.fontMaterialID);
                UnityEngine.Object.Destroy(fontMaterial);
                UnityEngine.Object.Destroy(dynamicFontAsset.texture);
                ecb.RemoveComponent<DynamicFontAsset>(entity);
            }
        }
        protected override void OnDestroy()
        {
            foreach (var (nativeFontPointer, entity) in SystemAPI.Query<NativeFontPointer>()
                .WithAll<NativeFontPointer>()
                .WithEntityAccess())
            {
                //Debug.Log($"Destroy Harfbuzz font pointer");
                nativeFontPointer.blob.Dispose();
                nativeFontPointer.face.Dispose();
                nativeFontPointer.font.Dispose();
            }
            
            //foreach (var (dynamicFontAsset, entity) in SystemAPI.Query<DynamicFontAsset>()
            //    .WithAll<DynamicFontAsset>()
            //    .WithEntityAccess())
            //{
            //    Debug.Log($"Destroy font material");
            //    if (dynamicFontAsset.blob.IsCreated) dynamicFontAsset.blob.Dispose();
            //    var fontMaterial = hybridRenderer.GetMaterial(dynamicFontAsset.fontMaterialID); //throws-->probably batch rendergroup already destroyed, so no need to destroy resources?
            //    hybridRenderer.UnregisterMaterial(dynamicFontAsset.fontMaterialID);
            //    UnityEngine.Object.Destroy(fontMaterial);
            //    UnityEngine.Object.Destroy(dynamicFontAsset.texture);
            //}
        }
    }
}
