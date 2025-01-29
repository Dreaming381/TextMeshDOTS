using TextMeshDOTS.HarfBuzz;
using Unity.Entities;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;
using Unity.Collections;
using TextMeshDOTS.HarfBuzz.Bitmap;
using Unity.Rendering;
using static TextMeshDOTS.TextCoreExtensions;
using System.IO;
using TextMeshDOTS.Rendering;
using Unity.Burst;
using System.Linq;
using System;


namespace TextMeshDOTS.TextProcessing
{
    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    partial struct NativeFontLoaderSystem : ISystem
    {
        EntityQuery textRendererQ, fontEntitiesQ, fontstateQ;
        NativeList<LoadRequest> newLoadRequests;
        EntityArchetype nativeFontDataArchetype, fontStateArchetype;
        DrawDelegates drawFunctions;
        PaintDelegates paintFunctions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            newLoadRequests = new NativeList<LoadRequest>(16, Allocator.Persistent);
            nativeFontDataArchetype = TextMeshDOTSArchetypes.GetNativeFontDataArchetype(ref state);

            fontStateArchetype = TextMeshDOTSArchetypes.GetFontStateArchetype(ref state);
            state.EntityManager.CreateEntity(fontStateArchetype);
            fontstateQ = SystemAPI.QueryBuilder()
                      .WithAll<FontState>()
                      .Build();

            textRendererQ = SystemAPI.QueryBuilder()
                .WithAll<FontBlobReference>()
                .WithAll<TextRenderControl>()
                .WithAll<MaterialMeshInfo>()
                .Build();

            fontEntitiesQ = SystemAPI.QueryBuilder()
                .WithAll<FontAssetRef>()
                .WithAll<FontAssetMetadata>()
                .WithAll<AtlasData>()
                .Build();

            drawFunctions = new DrawDelegates(true);
            paintFunctions =  new PaintDelegates(true);
            state.RequireForUpdate(fontstateQ);
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var existingFonts = fontEntitiesQ.ToComponentDataArray<FontAssetRef>(Allocator.TempJob);
            foreach (var fontBlobReference in SystemAPI.Query<FontBlobReference>()
                .WithAll<FontBlobReference>()
                .WithAll<TextRenderControl>()
                .WithChangeFilter<FontBlobReference>())                
            {
                //Debug.Log($"Font Reference has changed!");
                ref var fontBlob = ref fontBlobReference.value.Value;
                if (!existingFonts.Contains(fontBlob.fontAssetRef))
                {
                    var loadRequest = new LoadRequest { fontBlobRef = fontBlobReference, fontAssetRef= fontBlob.fontAssetRef };
                    if (!fontBlob.useSystemFont)
                    {
                        loadRequest.filePath = fontBlob.fontAssetPath;
                        if (!File.Exists(fontBlob.fontAssetPath.ToString()))
                            Debug.Log($"Could not find font in {fontBlob.fontAssetPath}");
                        else if (!newLoadRequests.Contains(loadRequest))
                                newLoadRequests.Add(loadRequest);
                    }
                    else
                    {
                        //loading rules: https://www.high-logic.com/fontcreator/manual15/fonttype.html
                        var typeographicFamilyDataMissing = (fontBlob.typographicFamily.IsEmpty || fontBlob.typographicSubfamily.IsEmpty);
                        var family = typeographicFamilyDataMissing ? fontBlob.fontFamily : fontBlob.typographicFamily;
                        var subFamily = typeographicFamilyDataMissing ? fontBlob.fontSubFamily : fontBlob.typographicSubfamily;
                        if (!TryGetSystemFontReference(family.ToString(), subFamily.ToString(), out UnityFontReference unityFontReference))
                            Debug.Log($"Could not find system font {family} {subFamily}");
                        else
                        {
                            loadRequest.filePath = unityFontReference.filePath;
                            if (!newLoadRequests.Contains(loadRequest))
                                newLoadRequests.Add(loadRequest);
                        }
                    }                     
                }                
            }
            if (newLoadRequests.Length == 0)
            {
                existingFonts.Dispose();
                return;
            }

            var fontStateEntity = fontstateQ.GetSingletonEntity();
            if (!SystemAPI.HasComponent<FontsDirtyTag>(fontStateEntity))
                state.EntityManager.AddComponent<FontsDirtyTag>(fontStateEntity);

            var allrequieredFonts = textRendererQ.ToComponentDataArray<FontBlobReference>(state.WorldUpdateAllocator);
            var allrequieredFontEntities = textRendererQ.ToEntityArray(state.WorldUpdateAllocator);

            //new fonts likely means that all MaterialReferences are wrong...so regenerate them
            //to do: find more elegant way to validate and patch up MaterialMeshInfo
            Debug.Log($"Regenerating all MaterialMeshInfo");
            state.EntityManager.RemoveComponent<MaterialMeshInfo>(textRendererQ);

            //validate if all existing fonts are still requiered
            var allRequieredFontsNR = new NativeHashMap<FontAssetRef,Entity>(256, state.WorldUpdateAllocator);
            for (int i = 0, ii = allrequieredFonts.Length; i < ii; i++)
            {
                var fontAssetRef = allrequieredFonts[i].value.Value.fontAssetRef;
                if (!allRequieredFontsNR.ContainsKey(fontAssetRef))
                    allRequieredFontsNR.Add(fontAssetRef, allrequieredFontEntities[i]);
            }            
            for (int i = 0, ii = existingFonts.Length; i < ii; i++)
            {
                var existingFont = existingFonts[i];
                if (!allRequieredFontsNR.TryGetValue(existingFont, out Entity item))
                {
                    Debug.Log("Destroy not needed font");
                    state.EntityManager.DestroyEntity(item); //can destroy existing font as it is not needed anymore by any of the active TextRenderer
                }
            }

            //load new fonts
            for (int i = 0, ii = newLoadRequests.Length; i < ii; i++)
                LoadFont(newLoadRequests[i], 48, 128, ref state);

            existingFonts.Dispose();
            newLoadRequests.Clear();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (newLoadRequests.IsCreated) newLoadRequests.Dispose();

            drawFunctions.Dispose();
            paintFunctions.Dispose();
        }
        void LoadFont(LoadRequest loadRequest, int samplingPointSizeSDF, int samplingPointSizeBitmap, ref SystemState state)
        {
            ref var fontBlobRef = ref loadRequest.fontBlobRef.value.Value;           

            var blob = new Blob(loadRequest.filePath.ToString());
            var face = new Face(blob.ptr, 0);
            var font = new Font(face.ptr);            

            var sdfOrientation = face.HasTrueTypeOutlines() ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;            

            var nativeFontPointer = new NativeFontPointer { 
                orientation = sdfOrientation, 
                blob = blob, 
                face = face, 
                font = font, 
                drawFunctions = drawFunctions,
                paintFunctions = paintFunctions,
            };

            var fontAssetMetadata = new FontAssetMetadata { family = fontBlobRef.fontFamily, subfamily = fontBlobRef.fontSubFamily };

            //initialize texture. To save space, review how to initialize it with size 0
            //(as done by TextCore), and only increase once needed
            Texture2D texture2D;
            DynamicFontAsset dynamicFontAsset;
            AtlasData atlasData;
            if (face.HasCOLR() || face.HasColorBitmap())
            {
                atlasData = new AtlasData
                {
                    atlasHeight = 1024,
                    atlasWidth = 1024,
                    padding = 0,                //10% of atlas height or width
                    samplingPointSize = samplingPointSizeBitmap,    //size of font (in pixel) in atlas
                };
                
                atlasData.padding = 0;
                texture2D = new Texture2D(atlasData.atlasWidth, atlasData.atlasHeight, TextureFormat.ARGB32, false);
                var textureData = texture2D.GetRawTextureData<ColorARGB>();
                for (int i = 0; i < textureData.Length; i++)
                    textureData[i] = (ColorARGB)Color.white;
                texture2D.Apply();
                dynamicFontAsset = new DynamicFontAsset { texture = texture2D, textureType = TextureType.ARGB };
            }
            else
            {
                atlasData = new AtlasData
                {
                    atlasHeight = 1024,
                    atlasWidth = 1024,
                    padding = 9,                //10% of atlas height or width
                    samplingPointSize = samplingPointSizeSDF,    //size of font (in pixel) in atlas
                };
                texture2D = new Texture2D(atlasData.atlasWidth, atlasData.atlasHeight, TextureFormat.Alpha8, false);
                var rawTextureData = texture2D.GetRawTextureData<byte>();

                //initialize to black
                for (int i = 0; i < rawTextureData.Length; i++)
                    rawTextureData[i] = 0;
                texture2D.Apply();
                dynamicFontAsset = new DynamicFontAsset { texture = texture2D, textureType = TextureType.SDF};
            }
            font.SetScale(atlasData.samplingPointSize, atlasData.samplingPointSize);

            var fontEntity = state.EntityManager.CreateEntity(nativeFontDataArchetype);
            state.EntityManager.SetComponentData(fontEntity, fontBlobRef.fontAssetRef);
            state.EntityManager.SetComponentData(fontEntity, fontAssetMetadata);            
            state.EntityManager.SetComponentData(fontEntity, atlasData);
            state.EntityManager.AddComponentData(fontEntity, dynamicFontAsset);
            state.EntityManager.AddComponentData(fontEntity, nativeFontPointer);

            var freeGlyphRects = state.EntityManager.GetBuffer<FreeGlyphRects>(fontEntity);
            NativeAtlas.InitialzeFreeGlyphRects(ref freeGlyphRects, atlasData.atlasWidth, atlasData.atlasHeight);        
        }

        public struct LoadRequest : IEquatable<LoadRequest>
        {
            public FontBlobReference fontBlobRef;
            public FixedString512Bytes filePath;
            public FontAssetRef fontAssetRef;
            public override bool Equals(object obj) => obj is FontAssetRef other && Equals(other);

            public bool Equals(LoadRequest other)
            {
                return GetHashCode() == other.GetHashCode();
            }
            public static bool operator ==(LoadRequest e1, LoadRequest e2)
            {
                return e1.GetHashCode() == e2.GetHashCode();
            }
            public static bool operator !=(LoadRequest e1, LoadRequest e2)
            {
                return e1.GetHashCode() != e2.GetHashCode();
            }
            public override int GetHashCode()
            {                
                return fontAssetRef.GetHashCode(); 
            }
        }
    }
}