using HarfBuzz;
using System.Linq;
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
                              .WithAll<HBFontAssetRef>()
                              .WithAll<FontTextureReference>()
                              .WithAll<GlyphsInUse>()
                              .WithAll<MissingGlyphs>()
                              .WithAll<CreatedFromFontAsset>()
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

            var entities = m_query.ToEntityArray(Allocator.Persistent);
            var newGlyphBlobs= new NativeList<GlyphBlob>(256, Allocator.Persistent);
            for (int i = 0, ii = entities.Length; i < ii; i++)
            {
                var entity= entities[i];
                var missingGlyphs = SystemAPI.GetBuffer<MissingGlyphs>(entity);

                if (missingGlyphs.Length > 0)
                {
                    var glyphsInUse = SystemAPI.GetBuffer<GlyphsInUse>(entity);
                    var fontTextureReference = SystemAPI.GetComponent<FontTextureReference>(entity);
                    var fontAsset = SystemAPI.GetComponent<CreatedFromFontAsset>(entity).fontAsset.Value;
                    Debug.Log($"Update Atlas for {fontTextureReference.blob.Value.familyName}");

                    //for (int j = 0, jj = missingGlyphs.Length; j < jj; j++)
                    for (int j = missingGlyphs.Length -1; j >=0; j--)
                    {
                        var missingGlyph = missingGlyphs[j];
                        if (fontAsset.TryAddGlyphInternal(missingGlyph.glyphID, out Glyph glyph))
                            newGlyphBlobs.Add(new GlyphBlob { glyphID = glyph.index, glyphExtents = (GlyphExtents)glyph.metrics, glyphRect = glyph.glyphRect });
                        else
                            Debug.Log($"Glyph {missingGlyph.glyphID} was not found in {fontAsset.name}");
                    }
                    if (newGlyphBlobs.Length > 0)
                    {
                        Debug.Log($"Patched {fontAsset.name}: added {missingGlyphs.Length} glyphs ");
                        glyphsInUse.Reinterpret<uint>().AddRange(missingGlyphs.Reinterpret<uint>().AsNativeArray());
                        fontTextureReference.blob.PatchDynamicFontData(newGlyphBlobs);
                        SystemAPI.SetComponent(entity, fontTextureReference);
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
