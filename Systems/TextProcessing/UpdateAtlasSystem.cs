using TextMeshDOTS.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore;

namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(ShapeSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class UpdateAtlasSystem : SystemBase
    {
        EntityQuery m_query;//, fontAssetQ;
        //EntitiesGraphicsSystem hybridRenderer;
        NativeHashSet<uint> glyphs;
        protected override void OnCreate()
        {
            //hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            m_query = SystemAPI.QueryBuilder()
                              .WithAll<FontAssetReference>()  
                              .WithAll<HBFontAssetReference>()
                              .WithAll<DynamicFontBlobReference>()
                              .WithAll<GlyphsInUse>()
                              .WithAll<MissingGlyphs>()
                              .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<MissingGlyphs>());
            //fontAssetQ = SystemAPI.QueryBuilder()
            //                  .WithAll<FontAssetReferences>()
            //                  .Build();
            //RequireForUpdate(fontAssetQ);
        }

        protected override void OnUpdate()
        {
            if (m_query.IsEmpty)
                return;

            var entities = m_query.ToEntityArray(Allocator.TempJob);
            var newGlyphBlobs= new NativeList<GlyphBlob>(256, Allocator.TempJob);
            for (int i = 0, ii = entities.Length; i < ii; i++)
            {
                var entity= entities[i];
                var missingGlyphs = SystemAPI.GetBuffer<MissingGlyphs>(entity);

                if (missingGlyphs.Length > 0)
                {
                    var fontAsset = EntityManager.GetComponentObject<FontAssetReference>(entity).value;
                    Debug.Log($"Update Atlas for {fontAsset.name}");

                    var glyphsInUse = SystemAPI.GetBuffer<GlyphsInUse>(entity);
                    var hbFontAssetReference = SystemAPI.GetComponent<HBFontAssetReference>(entity);
                    
                    var dynamicFontBlobReference = SystemAPI.GetComponent<DynamicFontBlobReference>(entity);

                    for (int j = 0, jj = missingGlyphs.Length; j < jj; j++)
                    {
                        var missingGlyph = missingGlyphs[j];
                        if (fontAsset.TryAddGlyphInternal(missingGlyph.glyphID, out Glyph glyph))
                            newGlyphBlobs.Add(new GlyphBlob { glyphID = glyph.index, glyphMetrics = glyph.metrics, glyphRect = glyph.glyphRect, glyphScale = glyph.scale });
                        else
                            Debug.Log($"Glyph {missingGlyph.glyphID} was not found in {fontAsset.name}");
                    }
                    if (newGlyphBlobs.Length > 0)
                    {
                        Debug.Log($"Patched {fontAsset.name}: added {missingGlyphs.Length} glyphs ");
                        glyphsInUse.Reinterpret<uint>().AddRange(missingGlyphs.Reinterpret<uint>().AsNativeArray());
                        dynamicFontBlobReference.blob.PatchDynamicFontData(newGlyphBlobs);
                        SystemAPI.SetComponent(entity, dynamicFontBlobReference);
                    }
                    else
                        Debug.Log($"Nothing to patch {fontAsset.name}");
                    missingGlyphs.Clear();

                    
                }
            }
            entities.Dispose();
            newGlyphBlobs.Dispose();

            ////var missingGlyphs = fontAtlasInfo.missingGlyphs;

            //if (missingGlyphs.Length > 0)
            //{
            //    Debug.Log($"Update Atlas");
            //    var glyphAtlas = fontAtlasInfo.glyphAtlas;

            //    var entities = m_query.ToEntityArray(WorldUpdateAllocator);
            //    var entity = entities[0];

            //    var font = EntityManager.GetComponentObject<FontAssetReferences>(entity).value;


            //    var existingMaterialMeshInfo = SystemAPI.GetComponent<MaterialMeshInfo>(entity);
            //    if (existingMaterialMeshInfo.Material >= 0) //is runtime Material
            //    {
            //        hybridRenderer.GetMaterial(existingMaterialMeshInfo.MaterialID);
            //        hybridRenderer.UnregisterMaterial(existingMaterialMeshInfo.MaterialID);
            //    }
            //    var brgMaterialID = hybridRenderer.RegisterMaterial(font.material);
            //    existingMaterialMeshInfo.MaterialID = brgMaterialID;
            //    SystemAPI.SetComponent<MaterialMeshInfo>(entity, existingMaterialMeshInfo);
            //}
        }

        protected override void OnDestroy()
        {
            glyphs.Dispose();
        }
    }
}
