using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using HarfBuzz;

namespace TextMeshDOTS.TextProcessing
{

    // To-Do: establish a Native OTF and TTF Font Resource Manager as in UnityEngine.TextCore.Text.TextResourceManager

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateBefore(typeof(ShapeSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class LoadNativeFont : SystemBase
    {
        EntityQuery m_query;
        Dictionary<string, NativeFont> m_fontDictionary; //this is local cache of FontManager.fontDictionary. Needed to ensure Dispose methods work reliably
        static readonly ProfilerMarker marker1 = new ProfilerMarker("load blob");
        static readonly ProfilerMarker marker2 = new ProfilerMarker("load face");
        static readonly ProfilerMarker marker3 = new ProfilerMarker("load font");

        protected override void OnCreate()
        {
            m_query = SystemAPI.QueryBuilder()
                              .WithAll<FontBlobReference>()
                              .WithAll<FontAssetReference>()
                              .WithNone<NativeFont>()
                              .Build();
            m_fontDictionary =new Dictionary<string, NativeFont>();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontBlobReference>());
            SystemAPI.TryGetSingletonRW<FontAtlasInfo>(out RefRW<FontAtlasInfo> fontAtlasInfo);
        }

        protected override void OnUpdate()
        {
            if (m_query.IsEmpty)
                return;

            if (!SystemAPI.TryGetSingletonEntity<FontAtlasInfo>(out Entity fontManagerEntity))
                return;
            
            var entities = m_query.ToEntityArray(Allocator.Temp);

            //Debug.Log($"fontAsset {fontAsset.name} sourceFontFile {fontAsset.sourceFontFile.name} fontSize {fontAsset.sourceFontFile.fontSize}");
            //string[] fontPaths = UnityEngine.Font.GetPathsToOSFonts();
            //foreach (var fontPath in fontPaths)
            //{
            //    Debug.Log(fontPath);
            //}
            FontManager fontManager;
            if (!EntityManager.HasComponent<FontManager>(fontManagerEntity))
            {
                fontManager =  new FontManager();
                fontManager.fontDictionary = new Dictionary<string, NativeFont>();
                fontManager.fontAssets = new Dictionary<string, FontAsset>();
                EntityManager.AddComponent<FontManager>(fontManagerEntity);
            }
            else
                fontManager = EntityManager.GetComponentObject<FontManager>(fontManagerEntity);


            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var fontBlobReference = EntityManager.GetComponentData<FontBlobReference>(entity);
                var fontAsset = EntityManager.GetComponentObject<FontAssetReference>(entity).value;
                ref var fontBlob = ref fontBlobReference.blob.Value;
                var fontName = fontBlob.name.ToString();

                if (!m_fontDictionary.TryGetValue(fontName, out NativeFont nativeFontReference))
                {
                    Debug.Log($"Load Font from Blob");
                    nativeFontReference = new NativeFont(ref fontBlob, (uint)fontBlob.nativeFontFile.Length);
                    m_fontDictionary.Add(fontName, nativeFontReference);
                    fontManager.fontAssets.Add(fontName, fontAsset);                    
                }
                else
                {
                    //Debug.Log($"Load exsiting NativeFontReference");
                }
                EntityManager.AddComponentData(entities[i], nativeFontReference);
            }
            fontManager.fontDictionary = m_fontDictionary;
            EntityManager.SetComponentData<FontManager>(fontManagerEntity, fontManager);




            //for (int i = 0; i < entities.Length; i++)
            //{
            //    var entity= entities[i];
            //    var fontAsset = EntityManager.GetComponentObject<FontAssetReference>(entity).value;
            //    var fontName = fontAsset.sourceFontFile.name;

            //    if (!m_fontDictionary.TryGetValue(fontName, out NativeFont nativeFontReference))
            //    {                    
            //        var fontOTFPath = $"Assets/Resources/{fontName}.otf";
            //        var fontTTFPath = $"Assets/Resources/{fontName}.ttf";
            //        if (File.Exists(fontOTFPath))
            //        {
            //            //Debug.Log($"Load OTF");
            //            nativeFontReference = new NativeFont(fontOTFPath);
            //            m_fontDictionary.Add(fontName, nativeFontReference);
            //            fontManager.fontAssets.Add(fontName, fontAsset);
            //        }
            //        else if (File.Exists(fontTTFPath))
            //        {
            //            //Debug.Log($"Load TTF");
            //            nativeFontReference = new NativeFont(fontTTFPath);
            //            m_fontDictionary.Add(fontName, nativeFontReference);
            //            fontManager.fontAssets.Add(fontName, fontAsset);
            //        }
            //    }
            //    else
            //    {
            //        //Debug.Log($"Load exsiting NativeFontReference");
            //    }
            //    EntityManager.AddComponentData(entities[i], nativeFontReference);                
            //}
            //fontManager.fontDictionary = m_fontDictionary;
            //EntityManager.SetComponentData<FontManager>(fontManagerEntity, fontManager);
        }

        protected override void OnDestroy()
        {
            Debug.Log($"Dispose {m_fontDictionary.Count} Fonts");
            foreach(var nativeFontReference in m_fontDictionary.Values)
            {
                nativeFontReference.nativeFont.Dispose();
                nativeFontReference.nativeFace.Dispose();
                nativeFontReference.nativeBlob.Dispose();
            }
            m_fontDictionary.Clear();
        }
    }
}
