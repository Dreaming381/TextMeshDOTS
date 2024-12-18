using HarfBuzz;
using System;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Font = HarfBuzz.Font;
using Unity.Collections;
using Unity.Profiling;
using HarfBuzz.SDF;
using Unity.Jobs;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.TextProcessing
{
    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(ShapeSystem))]
    partial struct NativeFontManagerSystem : ISystem
    {
        EntityQuery fontEntityQ;

        static readonly ProfilerMarker marker = new ProfilerMarker("GlyphRect");
        static readonly ProfilerMarker marker2 = new ProfilerMarker("SDF");

        //review: do we need to suppress GC collection of this object?
        IntPtr haffBuzzDrawFunct;

        EntityArchetype nativeFontDataArchetype;

        //[BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            nativeFontDataArchetype = TextMeshDOTSArchetypes.GetNativeFontDataArchetype(ref state);

            fontEntityQ = SystemAPI.QueryBuilder()
                .WithAll<HBFontAssetRef>()
                .WithAll<HBMissingGlyphs>()
                .WithAll<HBUsedGlyphs>()
                .WithAll<HBUsedGlyphRects>()
                .WithAll<HBFreeGlyphRects>()
                .WithAll<HBFontPointer>()
                .WithAll<FontTextureReference>()
                .WithAbsent<CreatedFromFontAsset>()
                .Build();

            //InitializeHarfBuzzDrawFunctions();

            //var fontPath = "Assets\\Resources\\Notosans\\NotoSansDisplay-Regular.ttf";
            //LoadFont(systemFontReferences[4].filePath, SDFOrientation.TRUETYPE, 50, ref state);
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if(fontEntityQ.IsEmpty) 
                return;

            var fontsRequiringUpdate = new NativeList<Entity>(16, Allocator.TempJob);
            foreach (var (hbFontAssetRef, hbMissingGlyphs, entity) in SystemAPI.Query<HBFontAssetRef, DynamicBuffer<HBMissingGlyphs>>()
                .WithAll<HBFontAssetRef>()
                .WithAll<HBMissingGlyphs>()
                .WithEntityAccess())
            {
                if (hbMissingGlyphs.Length > 0)
                {
                    fontsRequiringUpdate.Add(entity);
                    Debug.Log($"Missing {hbMissingGlyphs.Length} glyphs for font {hbFontAssetRef.family} (subfamily {hbFontAssetRef.subFamily}). Populating atlas...");
                }
            }
            if(fontsRequiringUpdate.IsEmpty)
            {
                fontsRequiringUpdate.Dispose();
                return;
            }

            state.Dependency.Complete();
            var placedGlyphs = new NativeList<GlyphBlob> (1024, Allocator.TempJob); 

            var hbFontAssetRefLookup = SystemAPI.GetComponentLookup<HBFontAssetRef>(true);
            var missingGlyphsLookup = SystemAPI.GetBufferLookup<HBMissingGlyphs>(false);
            var glyphsInUseLookup = SystemAPI.GetBufferLookup<HBUsedGlyphs>(false);
            var usedGlyphRectsLookup = SystemAPI.GetBufferLookup<HBUsedGlyphRects>(false);
            var freeGlyphRectsLookup = SystemAPI.GetBufferLookup<HBFreeGlyphRects>(false);

            var hbFontPointersLookup = SystemAPI.GetComponentLookup<HBFontPointer>(true);
            var fontTextureReferenceLookup = SystemAPI.GetComponentLookup<FontTextureReference>(false);

            for (int i = 0, ii = fontsRequiringUpdate.Length; i < ii; i++)
            {
                var fontEntity = fontsRequiringUpdate[i];
                
                //this managed call to texture object is reason why we cannot BURST compile the update method of this system
                var fontTextureReference = fontTextureReferenceLookup[fontEntity];
                var textureData = fontTextureReference.texture.Value.GetRawTextureData<byte>();

                var getGlyphRectsJob = new GetGlyphRectsJob()
                {
                    placedGlyphs = placedGlyphs,

                    fontEntity = fontEntity,
                    hbFontAssetRefLookup = hbFontAssetRefLookup,
                    hbFontPointerLookup = hbFontPointersLookup,

                    missingGlyphsBuffer = missingGlyphsLookup,
                    usedGlyphsBuffer = glyphsInUseLookup,
                    usedGlyphRectsBuffer = usedGlyphRectsLookup,
                    freeGlyphRectsBuffer = freeGlyphRectsLookup,
                };
                state.Dependency = getGlyphRectsJob.Schedule(state.Dependency);

                var updateAtlasTextureJob = new UpdateAtlasTextureJob()
                {
                    textureData = textureData,

                    fontEntity = fontEntity,
                    placedGlyphs = placedGlyphs,
                    hbFontAssetRefLookup = hbFontAssetRefLookup,
                    hbFontPointerLookup = hbFontPointersLookup,
                    usedGlyphsBuffer = glyphsInUseLookup,
                    usedGlyphRectsBuffer = usedGlyphRectsLookup,
                    marker = marker2,
                };
                state.Dependency = updateAtlasTextureJob.Schedule(placedGlyphs, 1, state.Dependency);

                var updateNativeFontJob = new UpdateNativeFontJob()
                {
                    fontTextureReferenceLookup = fontTextureReferenceLookup,

                    fontEntity = fontEntity,
                    hbFontAssetRefLookup = hbFontAssetRefLookup,
                    hbFontPointerLookup = hbFontPointersLookup,
                    placedGlyphs = placedGlyphs,
                };
                state.Dependency = updateNativeFontJob.Schedule(state.Dependency);

                state.Dependency.Complete();
                placedGlyphs.Clear();
                fontTextureReference.texture.Value.Apply();
            }
            fontsRequiringUpdate.Dispose(state.Dependency);
            placedGlyphs.Dispose(state.Dependency);

        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            HB.hb_draw_funcs_destroy(haffBuzzDrawFunct);
        }

        void LoadFont(string path, SDFOrientation orientation, int samplingPointSize, ref SystemState state)
        {
            //load font
            var blob = new Blob(path);
            var face = new Face(blob.ptr, 0);
            var font = new Font(face.ptr);
            font.SetScale(samplingPointSize, samplingPointSize);

            //fetch name of fontFamily and subFamily, generate hash code from that used to lookup this font
            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));
            var italicString = new FixedString32Bytes("Italic");
            
            var fontFamily = new FixedString128Bytes();            
            uint textSize = (uint)fontFamily.Capacity;
            face.GetFaceInfo(HB_OT_NAME_ID.FONT_FAMILY, language, ref textSize, ref fontFamily);
            fontFamily.Length = (int)textSize;

            var fontSubFamily = new FixedString128Bytes();
            textSize = (uint)fontFamily.Capacity;
            face.GetFaceInfo(HB_OT_NAME_ID.FONT_SUBFAMILY, language, ref textSize, ref fontSubFamily);
            fontSubFamily.Length = (int)textSize;

            var fontAssetRef = new FontAssetRef(TextHelper.GetHashCodeCaseInSensitive(fontFamily), TextFontWeight.Regular, fontSubFamily.Contains(italicString) ? Style.Italic : Style.Regular);
            var hbFontAssetRef = new HBFontAssetRef
            {
                family = fontFamily,
                subFamily = fontSubFamily,
                fontAssetRef = fontAssetRef,
                atlasHeight = 1024,
                atlasWidth = 1024,
                padding = 9,                //10% of atlas height or width
                samplingPointSize = 50,    //size of font (in pixel) in atlas
            };
            var hbFontPointer = new HBFontPointer {orientation = orientation, fontAssetRef = fontAssetRef, blob = blob, face = face, font = font, hbDrawFuncts = haffBuzzDrawFunct };

            //initialize texture. To save space, review how to initialize it with size 0
            //(as done by TextCore), and only increase once needed
            var texture2D = new Texture2D(hbFontAssetRef.atlasWidth, hbFontAssetRef.atlasHeight, TextureFormat.Alpha8, false);
            var rawTextureData = texture2D.GetRawTextureData<byte>();
            
            //initialize to black
            for (int i = 0; i < rawTextureData.Length; i++)
                rawTextureData[i] = 0;
            texture2D.Apply();
            var fontTextureReference = new FontTextureReference { texture = texture2D };

            var fontEntity = state.EntityManager.CreateEntity(nativeFontDataArchetype);
            state.EntityManager.SetComponentData(fontEntity, hbFontAssetRef);            
            state.EntityManager.AddComponentData(fontEntity, fontTextureReference);
            state.EntityManager.AddComponentData(fontEntity, hbFontPointer);

            var hbfreeGlyphRects = state.EntityManager.GetBuffer<HBFreeGlyphRects>(fontEntity);
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
    }
}