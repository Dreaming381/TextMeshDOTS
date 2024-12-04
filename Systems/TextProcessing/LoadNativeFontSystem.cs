using HarfBuzz;
using System.Collections.Generic;
using System.IO;
using TextMeshDOTS.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.TextProcessing
{
    // To-Do: establish a Native OTF and TTF Font Resource Manager as in UnityEngine.TextCore.Text.TextResourceManager

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateBefore(typeof(ExtractTextSegmentsSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class LoadNativeFont : SystemBase
    {
        EntityQuery newfontsQ;
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

            //Debug.Log($"Load Fonts {newfontsQ.CalculateEntityCount()}");

            //get data before doing any structural changes
            var entities = newfontsQ.ToEntityArray(Allocator.TempJob);
            var tmpFontMaterial = new NativeList<FontMaterial>(21, Allocator.TempJob);

            //review: is there no better way to establish ONE list of used fonts at baking time
            //to avoid reverse enginering this from all occurences of FontAssetReferences?
            for (int i = 0, ii = entities.Length; i < ii; i++)
            {
                var entity = entities[i];
                var fontAssets = EntityManager.GetComponentObject<FontAssetReferences>(entity).value;                   
                var fontBlobs = EntityManager.GetBuffer<FontBlobReference>(entity).ToNativeArray(Allocator.Temp);

                if (fontAssets.Count != fontBlobs.Length)
                    Debug.LogError($"Unexpected: managed und unmanaged font count does not match: {fontAssets.Count} {fontBlobs.Length}");

                for (int k = 0, kk= fontAssets.Count; k < kk; k++)
                {                    
                    var fontAsset = fontAssets[k];
                    if (!fontMaterialMap.TryGetValue(fontAsset.hashCode, out FontMaterial fontMaterial))
                        fontMaterial = CreatNewFontEntity(fontBlobs[k].blob, fontAsset, EntityManager);
                    tmpFontMaterial.Add(fontMaterial);
                }
                var fontMaterials = EntityManager.AddBuffer<FontMaterial>(entity);
                fontMaterials.AddRange(tmpFontMaterial.AsArray());

                //keep font references on master Font Entity used by RuntimeSpawner to continue to use it,
                //otherwise this just consumes chunk space on TextRenderer Entity for no reason (runtime uses FontMaterial)
                if (!HasBuffer<FontMaterialRef>(entity))
                {
                    EntityManager.RemoveComponent<FontAssetReferences>(entity);
                    EntityManager.RemoveComponent<FontBlobReference>(entity);
                }
                tmpFontMaterial.Clear();
            }

            entities.Dispose();
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

        FontMaterial CreatNewFontEntity(BlobAssetReference<FontBlob> fontBlobRef, FontAsset fontAsset, EntityManager entityManager)
        {
            //spawn new Font Entity, and initialize all data
            //Debug.Log($"Load {fontAsset.name}");
            var newFontEntity = entityManager.CreateEntity(runtimeFontDataArchetype);
            var usedGlyphs = entityManager.AddBuffer<GlyphsInUse>(newFontEntity);

            //To-To: figure out how to get path to system font or embedded asset font at runtime
            string filePath = null;
#if UNITY_EDITOR
            filePath = UnityEditor.AssetDatabase.GUIDToAssetPath(fontAsset.fontAssetCreationEditorSettings.sourceFontFileGUID);
#endif
            var hbFontAssetRef = new HBFontAssetReference(ref fontBlobRef.Value, filePath);
            var dynamicFontBlobRef = FontBlobber.CreateDynamicFontData(fontAsset, hbFontAssetRef, usedGlyphs.Reinterpret<uint>());
            var dynamicFontBlobReference = new DynamicFontBlobReference { blob = dynamicFontBlobRef };
            var fontMaterial = new FontMaterial(newFontEntity, fontBlobRef, dynamicFontBlobRef, hbFontAssetRef);

            var fontAssetRef = new FontAssetReference { value = fontAsset };

            entityManager.SetComponentData(newFontEntity, fontAssetRef);
            entityManager.SetComponentData(newFontEntity, hbFontAssetRef);
            entityManager.SetComponentData(newFontEntity, dynamicFontBlobReference);

            //Debug.Log($"Add {fontAsset.name} to FontManager");
            fontMaterialMap.Add(fontAsset.hashCode, fontMaterial);
            hbFontAssetRefMap.Add(fontAsset.hashCode, hbFontAssetRef);
            dynamicFontBlobMap.Add(fontAsset.hashCode, dynamicFontBlobReference);
            return fontMaterial;
        }
    }
}
