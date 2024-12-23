using HarfBuzz;
using System;
using Unity.Entities;
using UnityEngine;
using Font = HarfBuzz.Font;
using Unity.Collections;
using HarfBuzz.SDF;
using System.Collections.Generic;
using Unity.Rendering;
using static TextMeshDOTS.TextCoreExtensions;
using System.IO;
using TextMeshDOTS.Rendering;

namespace TextMeshDOTS.TextProcessing
{
    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(ShapeSystem))]
    partial class NativeFontLoaderSystem : SystemBase
    {
        EntityQuery textRendererQ, existingFontsQ;
        List<LoadRequest> newLoadRequests;
        EntityArchetype nativeFontDataArchetype;
        IntPtr haffBuzzDrawFunct;

        protected override void OnCreate()
        {
            newLoadRequests = new List<LoadRequest>();
            nativeFontDataArchetype = TextMeshDOTSArchetypes.GetNativeFontDataArchetype(ref CheckedStateRef);
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
            EntityManager.AddComponentData(SystemHandle, fontHashMap);

            SystemAPI.TryGetSingletonRW<FontHashMap>(out _);//still needed to create system dependency?

            InitializeHarfBuzzDrawFunctions();
        }

        //[BurstCompile]
        protected override void OnUpdate()
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
                        if (fontBlob.typographicFamily.IsEmpty || fontBlob.typographicSubfamily.IsEmpty)
                        {
                            if (!TextCoreExtensions.TryGetSystemFontReference(fontBlob.fontFamily.ToString(), fontBlob.fontSubFamily.ToString(), out UnityFontReference unityFontReference))
                                Debug.Log($"Could not find system font {fontBlob.fontFamily} {fontBlob.fontSubFamily}");
                            newLoadRequests.Add(new LoadRequest { fontBlobRef = fontBlobReference, filePath = unityFontReference.filePath });
                        }
                        else
                        {
                            if (!TextCoreExtensions.TryGetSystemFontReference(fontBlob.typographicFamily.ToString(), fontBlob.typographicSubfamily.ToString(), out UnityFontReference unityFontReference))
                                Debug.Log($"Could not find system font {fontBlob.typographicFamily} {fontBlob.typographicSubfamily}");
                            newLoadRequests.Add(new LoadRequest { fontBlobRef = fontBlobReference, filePath = unityFontReference.filePath });
                        }
                    }
                    else
                        newLoadRequests.Add(new LoadRequest { fontBlobRef = fontBlobReference });
                }                
            }
            if (this.newLoadRequests.Count == 0)
                return;

            fontHashMap.ValueRW.fontsDirty = true;
            EntityManager.DestroyEntity(existingFontsQ);
            //new fonts likely means that all MaterialReferences are wrong...so regenerate them
            EntityManager.RemoveComponent<MaterialMeshInfo>(textRendererQ);
            Debug.Log($"Regenerating all MaterialMeshInfo");

            //var systemFontReferences = TextCoreExtensions.GetSystemFontRef();
            //var test = UnityEngine.Font.GetPathsToOSFonts();
            //var test2 = UnityEngine.Font.GetOSInstalledFontNames();

            foreach (var loadRequest in this.newLoadRequests)
            {                
                LoadFont(loadRequest, 50, fontEntities);
            }
            newLoadRequests.Clear();

            fontHashMap = SystemAPI.GetSingletonRW<FontHashMap>();
            fontHashMap.ValueRW.fontEntities = fontEntities;
        }

        protected override void OnDestroy()
        {
            var fontHashMap = SystemAPI.GetSingleton<FontHashMap>();
            if (fontHashMap.fontEntities.IsCreated) fontHashMap.fontEntities.Dispose();
           
            HB.hb_draw_funcs_destroy(haffBuzzDrawFunct);
        }
        void LoadFont(LoadRequest loadRequest, int samplingPointSize,  NativeHashMap<FontAssetRef, Entity> fontEntities)
        {
            ref var fontBlobRef = ref loadRequest.fontBlobRef.value.Value;
            var nativeFileLength = (uint)fontBlobRef.nativeFontFile.Length;
            Blob blob;
            if (fontBlobRef.useSystemFont && loadRequest.filePath!=null && File.Exists(loadRequest.filePath))
            {
                blob = new Blob(loadRequest.filePath);
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
            font.SetScale(samplingPointSize, samplingPointSize);

            var sdfOrientation = face.HasTrueTypeOutlines() ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;

            var hbFontAssetRef = new AtlasData
            {
                atlasHeight = 1024,
                atlasWidth = 1024,
                padding = 9,                //10% of atlas height or width
                samplingPointSize = 50,    //size of font (in pixel) in atlas
            };
            var nativeFontPointer = new NativeFontPointer { orientation = sdfOrientation, blob = blob, face = face, font = font, hbDrawFuncts = haffBuzzDrawFunct };

            //initialize texture. To save space, review how to initialize it with size 0
            //(as done by TextCore), and only increase once needed
            var texture2D = new Texture2D(hbFontAssetRef.atlasWidth, hbFontAssetRef.atlasHeight, TextureFormat.Alpha8, false);
            var rawTextureData = texture2D.GetRawTextureData<byte>();

            //initialize to black
            for (int i = 0; i < rawTextureData.Length; i++)
                rawTextureData[i] = 0;
            texture2D.Apply();
            var fontTextureReference = new DynamicFontAssets { texture = texture2D };

            var fontEntity = EntityManager.CreateEntity(nativeFontDataArchetype);
            EntityManager.SetComponentData(fontEntity, loadRequest.fontBlobRef);
            EntityManager.SetComponentData(fontEntity, hbFontAssetRef);
            EntityManager.AddComponentData(fontEntity, fontTextureReference);
            EntityManager.AddComponentData(fontEntity, nativeFontPointer);

            var freeGlyphRects = EntityManager.GetBuffer<FreeGlyphRects>(fontEntity);
            NativeAtlas.InitialzeFreeGlyphRects(ref freeGlyphRects, hbFontAssetRef.atlasWidth, hbFontAssetRef.atlasHeight);            

            fontEntities.Add(loadRequest.fontBlobRef.value.Value.fontAssetRef, fontEntity);            
        }
        void InitializeHarfBuzzDrawFunctions()
        {
            haffBuzzDrawFunct = HB.hb_draw_funcs_create();
            var moveToDelegate = (MoveToDelegate)HBDelegateProxies.HBDraw_MoveTo;
            var lineToDelegate = (MoveToDelegate)HBDelegateProxies.HBDraw_LineTo;
            var quadraticToDelegate = (QuadraticToDelegate)HBDelegateProxies.HBDraw_QuadraticTo;
            var cubicToDelegate = (CubicToDelegate)HBDelegateProxies.HBDraw_CubicTo;
            var releaseDelegate = (ReleaseDelegate)null;// HBDelegateProxies.Test;

            HB.hb_draw_funcs_set_move_to_func(haffBuzzDrawFunct, moveToDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_draw_funcs_set_line_to_func(haffBuzzDrawFunct, lineToDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_draw_funcs_set_quadratic_to_func(haffBuzzDrawFunct, quadraticToDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_draw_funcs_set_cubic_to_func(haffBuzzDrawFunct, cubicToDelegate, IntPtr.Zero, releaseDelegate);
        }
        public struct LoadRequest
        {
            public FontBlobReference fontBlobRef;
            public string filePath;
        }
    }
}