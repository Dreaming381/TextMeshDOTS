using HarfBuzz;
using TextMeshDOTS.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS.TextProcessing
{
    // To-Do: establish a Native OTF and TTF Font Resource Manager as in UnityEngine.TextCore.Text.TextResourceManager

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateBefore(typeof(ExtractTextSegmentsSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class LoadNativeFontFromFontAssetSystem : SystemBase
    {
        EntityQuery textRendererQ;
        EntityArchetype nativeFontDataArchetype;


        //the following is needed to ensure disposal of runtime created Blobassets and Harfbuzz fonts
        //works reliably (query EntityWorld in OnDestroy is too late to reliably get all these components)
        NativeHashMap<int, FontEntity> fontEntityMap;

        //static readonly ProfilerMarker marker1 = new ProfilerMarker("load blob");
        //static readonly ProfilerMarker marker2 = new ProfilerMarker("load face");
        //static readonly ProfilerMarker marker3 = new ProfilerMarker("load font");

        protected override void OnCreate()
        {
            textRendererQ = SystemAPI.QueryBuilder()
                              .WithAll<FontBlobReference>()                              
                              .WithNone<GlyphsInUse>()
                              .Build();
            textRendererQ.SetChangedVersionFilter(typeof(FontBlobReference));

            nativeFontDataArchetype = TextMeshDOTSArchetypes.GetNativeFontDataArchetype(ref CheckedStateRef);

            fontEntityMap = new NativeHashMap<int, FontEntity>(256, Allocator.Persistent);            
        }

        protected override void OnUpdate()
        {
            if (textRendererQ.IsEmpty)
                return;

            //Debug.Log($"Load Fonts {textRendererQ.CalculateEntityCount()} (already {fontMaterialMap.Count} fonts loaded)");

            //get data before doing any structural changes
            var entities = textRendererQ.ToEntityArray(Allocator.TempJob);
            var tmpFontMaterial = new NativeList<FontEntity>(32, Allocator.TempJob);

            //review: is there no better way to establish ONE list of used fonts at baking time
            //to avoid reverse enginering this from all occurences of FontAssetReferences?
            for (int i = 0, ii = entities.Length; i < ii; i++)
            {
                var entity = entities[i];
                var fontBlobReferenceBuffer = EntityManager.GetBuffer<FontBlobReference>(entity).ToNativeArray(Allocator.Temp);

                for (int k = 0, kk= fontBlobReferenceBuffer.Length; k < kk; k++)
                {                    
                    var fontAsset = fontBlobReferenceBuffer[k].fontAsset.Value;
                    if (!fontEntityMap.TryGetValue(fontAsset.hashCode, out FontEntity fontMaterial))
                        fontMaterial = CreatNewFontEntity(fontBlobReferenceBuffer[k], fontAsset, EntityManager);
                    tmpFontMaterial.Add(fontMaterial);
                    //Debug.Log($"Load {fontAsset.faceInfo.familyName} {fontAsset.faceInfo.styleName}");
                }

                DynamicBuffer<FontEntity> fontEntities;
                if (EntityManager.HasBuffer<FontEntity>(entity))
                {                    
                    fontEntities = EntityManager.GetBuffer<FontEntity>(entity);
                    //Debug.Log($"Clear existing {fontMaterials.Length} Fonts ");
                    fontEntities.Clear();
                }
                else
                {
                    fontEntities = EntityManager.AddBuffer<FontEntity>(entity);                    
                }
                fontEntities.AddRange(tmpFontMaterial.AsArray());

                ////keep font references on master Font Entity used by RuntimeSpawner to continue to use it,
                ////otherwise this just consumes chunk space on TextRenderer Entity for no reason (runtime uses FontMaterial)
                //if (!HasBuffer<FontMaterialRef>(entity))
                //{
                //    EntityManager.RemoveComponent<FontBlobReference>(entity);
                //}
                tmpFontMaterial.Clear();
            }

            entities.Dispose();
            tmpFontMaterial.Dispose();
        }

        protected override void OnDestroy()
        {
            fontEntityMap.Dispose();
        }

        FontEntity CreatNewFontEntity(FontBlobReference fontBlobReference, FontAsset fontAsset, EntityManager entityManager)
        {
            //spawn new Font Entity, and initialize all data
            //Debug.Log($"Load {fontAsset.name}");
            var fontBlob = fontBlobReference.fontBlob;
            

            //To-To: figure out how to get path to system font or embedded asset font at runtime
            string filePath = null;
#if UNITY_EDITOR
            filePath = UnityEditor.AssetDatabase.GUIDToAssetPath(fontAsset.fontAssetCreationEditorSettings.sourceFontFileGUID);
#endif

            CreateNativeFont(filePath, out Blob blob, out Face face, out Font font);
            var entity = EntityManager.CreateEntity(nativeFontDataArchetype);
            var fontAssetRef = fontBlob.Value.fontAssetRef;
            var hbFontPointer = new HBFontPointer { family = fontBlob.Value.familyName, blob = blob, face = face, font = font };
            var hbFontAssetRef = new HBFontAssetRef { family = fontBlob.Value.familyName, subFamily = fontBlob.Value.styleName, fontAssetRef = fontAssetRef };

            EntityManager.SetComponentData(entity, hbFontAssetRef);
            EntityManager.AddComponentData(entity, hbFontPointer);            
            var usedGlyphs = entityManager.AddBuffer<GlyphsInUse>(entity);
            var dynamicFontBlobRef = FontBlobber.CreateDynamicFontData(fontAsset, face, font, fontBlob.Value.fontAssetRef, usedGlyphs.Reinterpret<uint>());
            var fontTextureReference = new FontTextureReference { texture = (Texture2D)fontAsset.material.mainTexture, blob = dynamicFontBlobRef };
            EntityManager.SetComponentData(entity, fontTextureReference);
            EntityManager.AddComponentData(entity, new CreatedFromFontAsset { fontAsset = fontAsset });
            //Debug.Log($"Add {fontAsset.name} to FontManager");
            var fontEntity = new FontEntity { value = entity };
            fontEntityMap.Add(fontAsset.hashCode, fontEntity);
            return fontEntity;
        }
        void CreateNativeFont(string fileName, out Blob blob, out Face face, out Font font)
        {
            //blob = new Blob(fontblob.nativeFontFile.GetUnsafePtr(), (uint)fontblob.nativeFontFile.Length, MemoryMode.Readonly);
            blob = new Blob(fileName);
            face = new Face(blob.ptr, 0);
            font = new Font(face.ptr);

            //in order to get the same scaled GlyphMetrics as Unity, we could set the scale to atlasSamplingPointSize,
            //but this would eliminate all units of precision as Harfbuzz works internaly with int
            //so better to do this correction during glyph generation
            //font.SetScale((int)fontblob.atlasSamplingPointSize, (int)fontblob.atlasSamplingPointSize);
            font.MakeImmutable();
        }
    }
}
