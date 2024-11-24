using HarfBuzz;
using System;
using TextMeshDOTS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;
using Font = HarfBuzz.Font;
using System.Collections.Generic;

namespace TextmeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateBefore(typeof(ShapeSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class LoadNativeFont : SystemBase
    {
        EntityQuery m_query;
        Dictionary<string, NativeFont> m_fontDictionary;
        protected override void OnCreate()
        {
            m_query = SystemAPI.QueryBuilder()
                              .WithAll<FontBlobReference>()
                              .WithAll<FontAssetReference>()
                              .WithNone<NativeFont>()
                              .Build();
            m_fontDictionary=new Dictionary<string, NativeFont>();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontBlobReference>());
        }

        protected override void OnUpdate()
        {
            if (m_query.IsEmpty)
                return;
            
            var entities = m_query.ToEntityArray(Allocator.Temp);            

            //Debug.Log($"fontAsset {fontAsset.name} sourceFontFile {fontAsset.sourceFontFile.name} fontSize {fontAsset.sourceFontFile.fontSize}");
            //string[] fontPaths = UnityEngine.Font.GetPathsToOSFonts();
            //foreach (var fontPath in fontPaths)
            //{
            //    Debug.Log(fontPath);
            //}

            for (int i = 0; i < entities.Length; i++)
            {
                var entity= entities[i];
                var fontName = EntityManager.GetComponentObject<FontAssetReference>(entity).value.sourceFontFile.name;

                if (!m_fontDictionary.TryGetValue(fontName, out NativeFont nativeFontReference))
                {
                    //Debug.Log($"Create new NativeFontReference");
                    var fontPath = $"Assets\\Resources\\{fontName}.ttf";
                    nativeFontReference = new NativeFont(fontPath);
                    m_fontDictionary.Add(fontName, nativeFontReference);
                }
                else
                {
                    //Debug.Log($"Load exsiting NativeFontReference");
                }
                EntityManager.AddComponentData(entities[i], nativeFontReference);
            }
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
        }
    }
}
