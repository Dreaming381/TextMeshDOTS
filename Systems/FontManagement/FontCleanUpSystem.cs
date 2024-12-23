using TextMeshDOTS;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace HarfBuzz
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

            foreach (var (hbFontPointer, entity) in SystemAPI.Query<NativeFontPointer>()
                .WithAll<NativeFontPointer>()
                .WithNone<UsedGlyphs>()
                .WithNone<MissingGlyphs>()          
                .WithEntityAccess())
            {                
                //Debug.Log($"Destroy Harfbuzz font with ID {hbFontPointer.d}");
                hbFontPointer.blob.Dispose();
                hbFontPointer.face.Dispose();
                hbFontPointer.font.Dispose();
                ecb.RemoveComponent<NativeFontPointer>(entity);
            }

            foreach (var (fontTextureReference, entity) in SystemAPI.Query<DynamicFontAssets>()
                .WithAll<DynamicFontAssets>()
                .WithNone<UsedGlyphs>()
                .WithNone<MissingGlyphs>()         
                .WithEntityAccess())
            {
                Debug.Log($"Destroy Font");
                fontTextureReference.blob.Dispose();
                var fontMaterial = hybridRenderer.GetMaterial(fontTextureReference.fontMaterialID);
                hybridRenderer.UnregisterMaterial(fontTextureReference.fontMaterialID);
                UnityEngine.Object.Destroy(fontMaterial);
                UnityEngine.Object.Destroy(fontTextureReference.texture);
                ecb.RemoveComponent<DynamicFontAssets>(entity);
            }
        }        
    }
}
