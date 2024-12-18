using HarfBuzz;
using System;
using Unity.Entities;
using UnityEngine;
using Font = HarfBuzz.Font;
using Unity.Collections;
using HarfBuzz.SDF;
using System.Collections.Generic;
using Unity.Rendering;

namespace TextMeshDOTS.TextProcessing
{
    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(ShapeSystem))]
    partial class GenerateFontLoadRequestsSystem : SystemBase
    {
        EntityQuery textQuery;
        List<LoadRequest> loadRequests;
        EntityArchetype nativeFontDataArchetype;
        IntPtr haffBuzzDrawFunct;

        protected override void OnCreate()
        {
            loadRequests = new List<LoadRequest>();
            nativeFontDataArchetype = TextMeshDOTSArchetypes.GetNativeFontDataArchetype(ref CheckedStateRef);
            textQuery = SystemAPI.QueryBuilder()
                .WithAll<FontBlobReference>()
                .WithAll<MaterialMeshInfo>()
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
            ref var fontEntities = ref fontHashMap.ValueRW.fontEntities;

            foreach (var (fontBlobReference, entity) in SystemAPI.Query<FontBlobReference>()
                .WithAll<FontBlobReference>()
                .WithEntityAccess()
                .WithChangeFilter<FontBlobReference>())
                
            {                
                var fontBlob = fontBlobReference.fontBlob;
                var fontAssetRef = fontBlob.Value.fontAssetRef;
                if (!fontEntities.ContainsKey(fontAssetRef))
                {
                    Debug.Log($"Request loading of {fontBlob.Value.familyName} {fontBlob.Value.styleName}");
                    if (!TextCoreExtensions.TryGetSystemFontReference(fontBlob.Value.familyName.ToString(), fontBlob.Value.styleName.ToString(), out UnityFontReference unityFontReference))
                        Debug.Log($"Could not find system font");

                    loadRequests.Add(new LoadRequest { unityFontReference = unityFontReference, fontAssetRef=fontAssetRef});
                }
                
            }
            if (loadRequests.Count == 0)
                return;

            fontHashMap.ValueRW.fontsDirty = true;
            //new fonts likely means that all MaterialReferences are wrong...so regenerate them
            EntityManager.RemoveComponent<MaterialMeshInfo>(textQuery);
            Debug.Log($"Regenerating all MaterialMeshInfo");

            //var systemFontReferences = TextCoreExtensions.GetSystemFontRef();
            //var test = UnityEngine.Font.GetPathsToOSFonts();
            //var test2 = UnityEngine.Font.GetOSInstalledFontNames();
            foreach (var loadRequest in loadRequests)
            {                
                LoadFont(loadRequest, ref fontEntities, 50);
            }
            loadRequests.Clear();
        }

        protected override void OnDestroy()
        {
            var fontHashMap = SystemAPI.GetSingleton<FontHashMap>();
            if (fontHashMap.fontEntities.IsCreated) fontHashMap.fontEntities.Dispose();
            HB.hb_draw_funcs_destroy(haffBuzzDrawFunct);
        }
        void LoadFont(LoadRequest loadRequest, ref NativeHashMap<FontAssetRef, Entity> fontEntities, int samplingPointSize)
        {
            var orientation = loadRequest.unityFontReference.filePath.EndsWith(".ttf") ||
                              loadRequest.unityFontReference.filePath.EndsWith(".TTF") ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;

            //load font
            var blob = new Blob(loadRequest.unityFontReference.filePath);
            var face = new Face(blob.ptr, 0);
            var font = new Font(face.ptr);
            font.SetScale(samplingPointSize, samplingPointSize);

            //fetch name of fontFamily and subFamily, generate hash code from that used to lookup this font
            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));

            var fontFamily = new FixedString128Bytes();
            uint textSize = (uint)fontFamily.Capacity;
            face.GetFaceInfo(HB_OT_NAME_ID.FONT_FAMILY, language, ref textSize, ref fontFamily);
            fontFamily.Length = (int)textSize;

            var fontSubFamily = new FixedString128Bytes();
            textSize = (uint)fontFamily.Capacity;
            face.GetFaceInfo(HB_OT_NAME_ID.FONT_SUBFAMILY, language, ref textSize, ref fontSubFamily);
            fontSubFamily.Length = (int)textSize;
            
            var hbFontAssetRef = new HBFontAssetRef
            {
                family = fontFamily,
                subFamily = fontSubFamily,
                fontAssetRef = loadRequest.fontAssetRef,
                atlasHeight = 1024,
                atlasWidth = 1024,
                padding = 9,                //10% of atlas height or width
                samplingPointSize = 50,    //size of font (in pixel) in atlas
            };
            var hbFontPointer = new HBFontPointer { orientation = orientation, fontAssetRef = loadRequest.fontAssetRef, blob = blob, face = face, font = font, hbDrawFuncts = haffBuzzDrawFunct };

            //initialize texture. To save space, review how to initialize it with size 0
            //(as done by TextCore), and only increase once needed
            var texture2D = new Texture2D(hbFontAssetRef.atlasWidth, hbFontAssetRef.atlasHeight, TextureFormat.Alpha8, false);
            var rawTextureData = texture2D.GetRawTextureData<byte>();

            //initialize to black
            for (int i = 0; i < rawTextureData.Length; i++)
                rawTextureData[i] = 0;
            texture2D.Apply();
            var fontTextureReference = new FontTextureReference { texture = texture2D };

            var fontEntity = EntityManager.CreateEntity(nativeFontDataArchetype);
            fontEntities.Add(loadRequest.fontAssetRef, fontEntity);
            EntityManager.SetComponentData(fontEntity, hbFontAssetRef);
            EntityManager.AddComponentData(fontEntity, fontTextureReference);
            EntityManager.AddComponentData(fontEntity, hbFontPointer);

            var hbfreeGlyphRects = EntityManager.GetBuffer<HBFreeGlyphRects>(fontEntity);
            NativeAtlas.InitialzeFreeGlyphRects(ref hbfreeGlyphRects, hbFontAssetRef.atlasWidth, hbFontAssetRef.atlasHeight);

            //var result = new FixedString128Bytes();
            //var values = Enum.GetValues(typeof(HB_OT_NAME_ID));
            //foreach (HB_OT_NAME_ID value in values)
            //{
            //    textSize = (uint)result.Capacity;
            //    face.GetFaceInfo(value, language, ref textSize, ref result);
            //    result.Length = (int)textSize;
            //    Debug.Log($"{value}: {result}");
            //    result.Clear();                
            //}
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
        internal struct LoadRequest
        {
            public FontAssetRef fontAssetRef;
            public UnityFontReference unityFontReference;
        }
    }
}