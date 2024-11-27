using System.Collections.Generic;
using TextMeshDOTS.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.TextProcessing
{
    // To-Do: establish a Native OTF and TTF Font Resource Manager as in UnityEngine.TextCore.Text.TextResourceManager

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateBefore(typeof(ExtractTextSegmentsSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class LoadNativeFont : SystemBase
    {
        EntityQuery newfontsQ, fontEntityQ;
        EntityArchetype runtimeFontDataArchetype;

        //the following is needed to ensure disposal of runtime created Blobassets and Harfbuzz fonts
        //works reliably (query EntityWorld in OnDestroy is too late to reliably get all these components)
        Dictionary<int, FontAsset> managedFontAssets;
        NativeHashMap<int, FontMaterial> fontMaterialMap;
        NativeHashMap<int, HBFontAssetReference> hbFontAssetRefMap;
        NativeHashMap<int, DynamicFontBlobReference> dynamicFontBlobMap;

        //static readonly ProfilerMarker marker1 = new ProfilerMarker("load blob");
        //static readonly ProfilerMarker marker2 = new ProfilerMarker("load face");
        //static readonly ProfilerMarker marker3 = new ProfilerMarker("load font");

        protected override void OnCreate()
        {
            newfontsQ = SystemAPI.QueryBuilder()
                              .WithAll<FontAssetReferences>()
                              .WithAll<FontBlobReference>()                              
                              .WithNone<FontMaterial>()
                              .Build();
            fontEntityQ = SystemAPI.QueryBuilder()
                              .WithAll<GlyphsInUse>()
                              .WithAll<MissingGlyphs>()
                              .WithAll<HBFontAssetReference>()
                              .WithAll<DynamicFontBlobReference>()
                              .Build();

            runtimeFontDataArchetype = TextMeshDOTSArchetypes.GetRuntimeFontDataArchetype(ref CheckedStateRef);

            managedFontAssets = new Dictionary<int, FontAsset>(256);
            fontMaterialMap = new NativeHashMap<int, FontMaterial>(256, Allocator.Persistent);
            hbFontAssetRefMap = new NativeHashMap<int, HBFontAssetReference>(256, Allocator.Persistent);
            dynamicFontBlobMap = new NativeHashMap<int, DynamicFontBlobReference>(256, Allocator.Persistent);

            //SystemAPI.TryGetSingletonRW<GlyphsInUse>(out RefRW<GlyphsInUse> fontAtlasInfo);
        }

        protected override void OnUpdate()
        {
            if (newfontsQ.IsEmpty)
                return;

            //get data before doing any structural changes
            var entities = newfontsQ.ToEntityArray(Allocator.TempJob);
            var existingFonts = fontEntityQ.ToComponentDataArray<HBFontAssetReference>(Allocator.TempJob);
            var tmpFontMaterial = new NativeList<FontMaterial>(21, Allocator.TempJob);

            // At the end of the update we patch up RuntimeFontDataEntity, so need to add component.
            // Do so via query overload as this cost virtually zero time
            //EntityManager.AddComponent<RuntimeFontDataEntity>(newTextRendererQ);

            //1st, collect unique fonts referenced by FontAssetReference
            //review: is there no better way to establish ONE list of used fonts at baking time
            //to avoid reverse enginering this from font usage?


            for (int i = 0, ii = entities.Length; i < ii; i++)
            {
                var entity = entities[i];
                var fontAssets = EntityManager.GetComponentObject<FontAssetReferences>(entity).value;
                var fontBlobs = EntityManager.GetBuffer<FontBlobReference>(entity).ToNativeArray(Allocator.Temp);

                if (fontAssets.Count != fontBlobs.Length)
                    Debug.LogError($"Unexpected: managed und unmanaged font count does not match: {fontAssets.Count} {fontBlobs.Length}");

                //var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
                //var ecb = ecbSingleton.CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
                
                for (int k = 0, kk= fontAssets.Count; k < kk; k++)
                {                    
                    var fontAsset = fontAssets[k];                    
                    if (!fontMaterialMap.TryGetValue(fontAsset.hashCode, out FontMaterial fontMaterial))
                    {
                        //spawn new Font Entity, and initialize all data
                        //Debug.Log($"Load {fontAsset.name}");
                        var newFontEntity = EntityManager.CreateEntity(runtimeFontDataArchetype);
                        var usedGlyphs = EntityManager.AddBuffer<GlyphsInUse>(newFontEntity);
                        
                        var fontBlobRef = fontBlobs[k].blob;
                        var hbFontAssetRef = new HBFontAssetReference(ref fontBlobRef.Value);
                        var dynamicFontBlobRef = FontBlobber.CreateDynamicFontData(fontAsset, hbFontAssetRef, usedGlyphs.Reinterpret<uint>());
                        var dynamicFontBlobReference = new DynamicFontBlobReference { blob = dynamicFontBlobRef };
                        fontMaterial = new FontMaterial(newFontEntity, fontBlobRef, dynamicFontBlobRef, hbFontAssetRef);

                        var fontAssetRef = new FontAssetReference { value = fontAsset };
                        EntityManager.SetComponentData(newFontEntity, fontAssetRef);
                        EntityManager.SetComponentData(newFontEntity, hbFontAssetRef);
                        EntityManager.SetComponentData(newFontEntity, dynamicFontBlobReference);
                        fontMaterialMap.Add(fontAsset.hashCode, fontMaterial);
                        hbFontAssetRefMap.Add(fontAsset.hashCode, hbFontAssetRef);
                        dynamicFontBlobMap.Add(fontAsset.hashCode, dynamicFontBlobReference);
                    }
                    tmpFontMaterial.Add(fontMaterial);
                }
                var fontMaterials = EntityManager.AddBuffer<FontMaterial>(entity);
                fontMaterials.AddRange(tmpFontMaterial.AsArray());
                tmpFontMaterial.Clear();
            }
            
            entities.Dispose();
            existingFonts.Dispose();
            tmpFontMaterial.Dispose();
        }

        protected override void OnDestroy()
        {
            //Debug.Log($"Dispose {fontMaterialMap.Count} Fonts");
            foreach(var hbFontAsset in hbFontAssetRefMap)
            {
                hbFontAsset.Value.font.Dispose();
                hbFontAsset.Value.face.Dispose();
                hbFontAsset.Value.blob.Dispose();
            }

            //need to destroy runtime generated dynamicFontBlob manually.
            //disposal of baked FontBlobs are managed by Unity
            foreach (var dynamicFontBlob in dynamicFontBlobMap)
            {
                if (dynamicFontBlob.Value.blob.IsCreated)
                    dynamicFontBlob.Value.blob.Dispose();
                else
                    Debug.LogError("Error disposing invalid reference");
            }
            managedFontAssets.Clear();
            fontMaterialMap.Dispose();
            hbFontAssetRefMap.Dispose();
            dynamicFontBlobMap.Dispose();
        }
    }
}
