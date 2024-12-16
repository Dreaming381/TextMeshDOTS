using TextMeshDOTS;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace HarfBuzz
{

    partial struct FontCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (hbFontPointer, entity) in SystemAPI.Query<HBFontPointer>()
                .WithAll<HBFontPointer>()
                .WithNone<HBGlyphsInUse>()
                .WithNone<HBMissingGlyphs>()          
                .WithEntityAccess())
            {                
                Debug.Log($"Destroy Harfbuzz font with ID {hbFontPointer.fontAssetRef}");
                hbFontPointer.blob.Dispose();
                hbFontPointer.face.Dispose();
                hbFontPointer.font.Dispose();
                ecb.RemoveComponent<HBFontPointer>(entity);
            }

            foreach (var (fontTextureReference, entity) in SystemAPI.Query<FontTextureReference>()
                .WithAll<FontTextureReference>()
                .WithNone<HBGlyphsInUse>()
                .WithNone<HBMissingGlyphs>()         
                .WithEntityAccess())
            {
                Debug.Log($"Destroy Font Blob");
                fontTextureReference.blob.Dispose();
                ecb.RemoveComponent<FontTextureReference>(entity);
            }
        }        
    }
}
