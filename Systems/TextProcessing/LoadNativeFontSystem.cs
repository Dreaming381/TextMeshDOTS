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
    //[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    //[RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(ShapeSystem))]
    partial struct LoadNativeFontSystem : ISystem
    {
        EntityQuery fontEntityQ;

        static readonly ProfilerMarker marker = new ProfilerMarker("Glyph");
        static readonly ProfilerMarker marker2 = new ProfilerMarker("Atlas");
        static readonly ProfilerMarker marker3 = new ProfilerMarker("SDF");

        UnityObjectRef<Texture2D> texture2D;
        NativeAtlas nativeAtlas;
        EntityArchetype nativeFontDataArchetype;
        int padding;//size of font in atlas
        int atlasSamplingPointSize;//size of font in atlas
        int atlasWidth;
        int atlasHeight;        
        NativeHashMap<int, FontTextureReference> fontTextureReferenceMap;

        NativeList<uint> missingGlyphs;

        //[BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontEntityQ = SystemAPI.QueryBuilder()
                .WithAll<HBFontAssetRef>()
                .WithAll<FontTextureReference>()
                .WithAll<GlyphsInUse>()
                .WithAll<MissingGlyphs>()
                .WithAll<HBFontPointer>()
                .WithAbsent<CreatedFromFontAsset>()
                .Build();

            padding = 9;
            atlasSamplingPointSize = 50;
            atlasWidth = atlasHeight = 1024;

            texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false);
            var rawTextureData = texture2D.Value.GetRawTextureData<byte>();
            for (int i = 0; i < rawTextureData.Length; i++)
                rawTextureData[i] = 0;
            texture2D.Value.Apply();

            nativeAtlas = new NativeAtlas(rawTextureData, 2048, atlasWidth, atlasHeight, Allocator.Persistent);
            fontTextureReferenceMap = new NativeHashMap<int, FontTextureReference>(256, Allocator.Persistent);
            nativeFontDataArchetype = TextMeshDOTSArchetypes.GetNativeFontDataArchetype(ref state);
            LoadFont(atlasSamplingPointSize, texture2D, ref state);

            //missingGlyphs = new NativeList<uint>(256, Allocator.Persistent);
            //for (uint i = 0; i < 430; i++)
            //    missingGlyphs.Add(i);
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if(fontEntityQ.IsEmpty) 
                return;
            state.Dependency.Complete();
            var fontEntities = fontEntityQ.ToEntityArray(Allocator.Persistent);
            var hbFontPointersLookup = SystemAPI.GetComponentLookup<HBFontPointer>(true);
            var hbFontAssetRefLookup = SystemAPI.GetComponentLookup<HBFontAssetRef>(true);
            var fontTextureReferenceLookup = SystemAPI.GetComponentLookup<FontTextureReference>(false);
            var glyphsInUseLookup = SystemAPI.GetBufferLookup<GlyphsInUse>(false);
            var missingGlyphsLookup = SystemAPI.GetBufferLookup<MissingGlyphs>(false);

            for (int i = 0, ii = fontEntities.Length; i < ii; i++)
            {
                nativeAtlas.Clear();
                var fontEntity = fontEntities[i];
                var missingGlyphs = missingGlyphsLookup[fontEntity].Reinterpret<uint>();
                if(missingGlyphs.Length == 0)
                    continue;                

                var fontTextureReference = fontTextureReferenceLookup[fontEntity];

                //this managed call to texture object is reason why we cannot BURST compile the update method of this system
                var textureData = fontTextureReference.texture.Value.GetRawTextureData<byte>();

                var hbFontAssetRef = hbFontAssetRefLookup[fontEntity];
                Debug.Log($"Missing {missingGlyphs.Length} glyphs for font {hbFontAssetRef.family} {hbFontAssetRef.subFamily}, populating atlas");

                var getGlyphRectsJob = new GetGlyphRectsJob()
                {
                    padding = padding,
                    fontEntity = fontEntity,
                    hbFontPointerLookup = hbFontPointersLookup,
                    placedGlyphs = nativeAtlas.placedGlyphs,
                    freeRects = nativeAtlas.freeRects,
                    usedRects = nativeAtlas.usedRects,
                    glyphIDs = missingGlyphs,
                };
                state.Dependency = getGlyphRectsJob.Schedule(state.Dependency);

                var populateAtlasTextureJob = new PopulateAtlasTextureJob()
                {
                    padding = padding,
                    fontEntity = fontEntity,
                    hbFontPointerLookup = hbFontPointersLookup,
                    atlasWidth = nativeAtlas.atlasWidth,
                    atlasHeight = nativeAtlas.atlasHeight,
                    placedGlyphs = nativeAtlas.placedGlyphs,
                    usedRects = nativeAtlas.usedRects,
                    textureData = textureData,
                    marker = marker3,
                };
                state.Dependency = populateAtlasTextureJob.Schedule(nativeAtlas.placedGlyphs, 1, state.Dependency);

                var spawnNativeFontJob = new SpawnNativeFontJob()
                {
                    fontEntity = fontEntity,
                    hbFontPointerLookup = hbFontPointersLookup,
                    hbFontAssetRefLookup = hbFontAssetRefLookup,
                    fontTextureReferenceLookup = fontTextureReferenceLookup,
                    glyphsInUseLookup = glyphsInUseLookup,
                    atlasSamplingPointSize = atlasSamplingPointSize,
                    atlasWidth = atlasWidth,
                    atlasHeight = atlasHeight,
                    padding = padding,
                    //fontTextureReferenceMap = fontTextureReferenceMap,
                    hbGlyphs = nativeAtlas.placedGlyphs,
                    //texture2D = texture2D,
                };
                state.Dependency = spawnNativeFontJob.Schedule(state.Dependency);
                state.Dependency.Complete();
                fontTextureReference.texture.Value.Apply();
            }
            fontEntities.Dispose();
            //glyphIDs.Dispose(state.Dependency);
            //state.Enabled = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            //blob.Dispose();
            //face.Dispose();
            //font.Dispose();
            //HB.hb_draw_funcs_destroy(drawFunct);
            foreach (var fontTextureReference in fontTextureReferenceMap)
            {
                if (fontTextureReference.Value.blob.IsCreated)
                    fontTextureReference.Value.blob.Dispose();
                else
                    Debug.LogError("Error disposing invalid reference");
            }
            nativeAtlas.Dispose();
            fontTextureReferenceMap.Dispose();
            //if(missingGlyphs.IsCreated) missingGlyphs.Dispose();
        }
        void LoadFont(int pointSize, Texture2D texture2D, ref SystemState state)
        {
            var blob = new Blob("Assets\\Resources\\Notosans\\NotoSansDisplay-Regular.ttf");
            var orientation = SDFOrientation.TRUETYPE;

            var face = new Face(blob.ptr, 0);
            var font = new Font(face.ptr);
            font.SetScale(pointSize, pointSize);

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

            var drawFunct = HB.hb_draw_funcs_create();
            var moveToDelegate = (MoveToDelegate)HBDelegateProxies.HBDraw_MoveTo;
            var lineToDelegate = (MoveToDelegate)HBDelegateProxies.HBDraw_LineTo;
            var quadraticToDelegate = (QuadraticToDelegate)HBDelegateProxies.HBDraw_QuadraticTo;
            var cubicToDelegate = (CubicToDelegate)HBDelegateProxies.HBDraw_CubicTo;
            var releaseDelegate = (ReleaseDelegate)null;// HBDelegateProxies.Test;

            HB.hb_draw_funcs_set_move_to_func(drawFunct, moveToDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_draw_funcs_set_line_to_func(drawFunct, lineToDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_draw_funcs_set_quadratic_to_func(drawFunct, quadraticToDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_draw_funcs_set_cubic_to_func(drawFunct, cubicToDelegate, IntPtr.Zero, releaseDelegate);

            var fontEntity = state.EntityManager.CreateEntity(nativeFontDataArchetype);
            var fontAssetRef = new FontAssetRef(TextHelper.GetHashCodeCaseInSensitive(fontFamily), TextFontWeight.Regular, fontSubFamily.Contains(italicString));
            var hbFontPointer = new HBFontPointer { family = fontFamily, orientation = orientation,  fontAssetRef = fontAssetRef, blob = blob, face = face, font = font, hbDrawFuncts = drawFunct };
            var hbFontAssetRef = new HBFontAssetRef { family = fontFamily, subFamily = fontSubFamily, fontAssetRef = fontAssetRef };
            var fontTextureReference = new FontTextureReference { texture = texture2D };

            state.EntityManager.SetComponentData(fontEntity, hbFontAssetRef);
            state.EntityManager.AddComponentData(fontEntity, hbFontPointer);
            state.EntityManager.SetComponentData(fontEntity, fontTextureReference);

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
    }
}