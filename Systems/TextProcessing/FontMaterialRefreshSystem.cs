using HarfBuzz;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(GlyphHashmapSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class FontMaterialRefreshSystem : SystemBase
    {
        EntityQuery m_query;
        EntitiesGraphicsSystem hybridRenderer;
        bool fontRebuild;
        List<string> fontNames;
        protected override void OnCreate()
        {
            fontNames=new List<string>();
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            m_query = SystemAPI.QueryBuilder()
                              .WithAll<GlyphOTF>()
                              .WithAll<FontAssetReference>()
                              .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<GlyphOTF>());

            SystemAPI.TryGetSingletonRW<FontAtlasInfo>(out RefRW<FontAtlasInfo> fontManager);
            UnityEngine.Font.textureRebuilt += OnFontAtlasRebuild;
        }

        protected override void OnUpdate()
        {
            if(fontRebuild)
            {
                if (!SystemAPI.TryGetSingletonEntity<FontAtlasInfo>(out Entity fontManagerEntity))
                    return;
                
                if (EntityManager.HasComponent<FontManager>(fontManagerEntity))
                {
                    var fontManager = EntityManager.GetComponentObject<FontManager>(fontManagerEntity);
                    for (int i = 0, ii = fontNames.Count; i < ii; i++)
                    {
                        var fontName = fontNames[i];
                        if (fontManager.fontAssets.ContainsKey(fontName))
                        {
                            Debug.Log($"{fontName} was found in fontManager");
                        
                        }
                        //else
                        //    Debug.LogWarning($"{fontName} NOT found in fontManager");
                    }
                }

                fontNames.Clear();
                fontRebuild = false;
            }
        }

        protected override void OnDestroy()
        {
        }
        void OnFontAtlasRebuild(UnityEngine.Font font)
        {
            //Debug.Log($"{font.name} was rebuild");
            fontNames.Add(font.name);
            fontRebuild = true;            
        }
    }
}
