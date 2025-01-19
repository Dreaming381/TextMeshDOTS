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

namespace TextMeshDOTS.TextProcessing
{
    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(ShapeSystem))]
    partial struct NativeFontLoaderSystem : ISystem
    {
        EntityQuery textRendererQ, existingFontsQ;
        NativeList<LoadRequest> newLoadRequests;
        EntityArchetype nativeFontDataArchetype;
        DrawDelegates drawFunctions;
        PaintDelegates paintFunctions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            newLoadRequests = new NativeList<LoadRequest>(16, Allocator.Persistent);
            nativeFontDataArchetype = TextMeshDOTSArchetypes.GetNativeFontDataArchetype(ref state);
            textRendererQ = SystemAPI.QueryBuilder()
                .WithAll<FontBlobReference>()
                .WithAll<TextRenderControl>()
                .WithAll<MaterialMeshInfo>()
                .Build();

            existingFontsQ = SystemAPI.QueryBuilder()
                .WithAll<FontBlobReference>()
                .WithAll<AtlasData>()
                .Build();

            var fontHashMap = new FontHashMap
            {
                fontEntities = new NativeHashMap<FontAssetRef, Entity>(16, Allocator.Persistent),
            };
            state.EntityManager.AddComponentData(state.SystemHandle, fontHashMap);

            SystemAPI.TryGetSingletonRW<FontHashMap>(out _);//still needed to create system dependency?

            drawFunctions = new DrawDelegates(true);
            paintFunctions =  new PaintDelegates(true);
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fontHashMap = SystemAPI.GetSingletonRW<FontHashMap>();
            var fontEntities = fontHashMap.ValueRO.fontEntities;

            foreach (var (fontBlobReference, entity) in SystemAPI.Query<FontBlobReference>()
                .WithAll<FontBlobReference>()
                .WithAll<TextRenderControl>()
                .WithEntityAccess()
                .WithChangeFilter<FontBlobReference>())
                
            {
                ref var fontBlob = ref fontBlobReference.value.Value;

                if (!fontEntities.ContainsKey(fontBlob.fontAssetRef))
                {                    
                    if (fontBlob.useSystemFont)
                    {
                        //loading rules: https://www.high-logic.com/fontcreator/manual15/fonttype.html
                        if (fontBlob.typographicFamily.IsEmpty || fontBlob.typographicSubfamily.IsEmpty)
                        {
                            if (!TextCoreExtensions.TryGetSystemFontReference(fontBlob.fontFamily.ToString(), fontBlob.fontSubFamily.ToString(), out UnityFontReference unityFontReference))
                                Debug.Log($"Could not find system font {fontBlob.fontFamily} {fontBlob.fontSubFamily}");
                            else
                                newLoadRequests.Add(new LoadRequest { fontBlobRef = fontBlobReference, filePath = unityFontReference.filePath });
                        }
                        else
                        {
                            if (!TextCoreExtensions.TryGetSystemFontReference(fontBlob.typographicFamily.ToString(), fontBlob.typographicSubfamily.ToString(), out UnityFontReference unityFontReference))
                                Debug.Log($"Could not find system font {fontBlob.typographicFamily} {fontBlob.typographicSubfamily}");
                            else
                                newLoadRequests.Add(new LoadRequest { fontBlobRef = fontBlobReference, filePath = unityFontReference.filePath });
                        }
                    }
                    else
                        newLoadRequests.Add(new LoadRequest { fontBlobRef = fontBlobReference });
                }                
            }
            if (this.newLoadRequests.Length == 0)
                return;

            fontHashMap.ValueRW.fontsDirty = true;
            state.EntityManager.DestroyEntity(existingFontsQ);
            //new fonts likely means that all MaterialReferences are wrong...so regenerate them
            state.EntityManager.RemoveComponent<MaterialMeshInfo>(textRendererQ);
            Debug.Log($"Regenerating all MaterialMeshInfo");

            //var systemFontReferences = TextCoreExtensions.GetSystemFontRef();
            //var test = UnityEngine.Font.GetPathsToOSFonts();
            //var test2 = UnityEngine.Font.GetOSInstalledFontNames();

            foreach (var loadRequest in this.newLoadRequests)
            {                
                LoadFont(loadRequest, 48, 128, fontEntities, ref state);
            }
            newLoadRequests.Clear();

            fontHashMap = SystemAPI.GetSingletonRW<FontHashMap>();
            fontHashMap.ValueRW.fontEntities = fontEntities;
        }

        public void OnDestroy(ref SystemState state)
        {
            var fontHashMap = SystemAPI.GetSingleton<FontHashMap>();
            if (fontHashMap.fontEntities.IsCreated) fontHashMap.fontEntities.Dispose();
            if (newLoadRequests.IsCreated) newLoadRequests.Dispose();

            drawFunctions.Dispose();
            paintFunctions.Dispose();
        }
        void LoadFont(LoadRequest loadRequest, int samplingPointSizeSDF, int samplingPointSizeBitmap, NativeHashMap<FontAssetRef, Entity> fontEntities, ref SystemState state)
        {
            ref var fontBlobRef = ref loadRequest.fontBlobRef.value.Value;
            var nativeFileLength = (uint)fontBlobRef.nativeFontFile.Length;
            Blob blob;
            if (fontBlobRef.useSystemFont && loadRequest.filePath!=null && File.Exists(loadRequest.filePath.ToString()))
            {
                blob = new Blob(loadRequest.filePath.ToString());
            }
            else
            {
                if (nativeFileLength == 0)
                {
                    Debug.Log("Font could neither be loaded from system nor from blob");
                    return;
                }
                unsafe
                {                    
                    blob = new Blob(fontBlobRef.nativeFontFile.GetUnsafePtr(), (uint)fontBlobRef.nativeFontFile.Length, MemoryMode.Readonly);
                }
            }
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
                dynamicFontAsset = new DynamicFontAsset { texture = texture2D, textureType = TextureType.SDF };
            }
            font.SetScale(atlasData.samplingPointSize, atlasData.samplingPointSize);


            var fontEntity = state.EntityManager.CreateEntity(nativeFontDataArchetype);
            state.EntityManager.SetComponentData(fontEntity, loadRequest.fontBlobRef);
            state.EntityManager.SetComponentData(fontEntity, atlasData);
            state.EntityManager.AddComponentData(fontEntity, dynamicFontAsset);
            state.EntityManager.AddComponentData(fontEntity, nativeFontPointer);

            var freeGlyphRects = state.EntityManager.GetBuffer<FreeGlyphRects>(fontEntity);
            NativeAtlas.InitialzeFreeGlyphRects(ref freeGlyphRects, atlasData.atlasWidth, atlasData.atlasHeight);            

            fontEntities.Add(loadRequest.fontBlobRef.value.Value.fontAssetRef, fontEntity);            
        }

        public struct LoadRequest
        {
            public FontBlobReference fontBlobRef;
            public FixedString512Bytes filePath;
        }
    }
}