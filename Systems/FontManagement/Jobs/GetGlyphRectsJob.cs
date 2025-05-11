using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore;
using TextMeshDOTS.HarfBuzz.Bitmap;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using TextMeshDOTS.HarfBuzz;


namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct GetGlyphRectsJob : IJob
    {
        public NativeList<GlyphBlob> placedGlyphs;

        public Entity fontEntity;

        [ReadOnly] public ComponentLookup<AtlasData> atlasDataLookup;
        [ReadOnly] public FontTable fontTable;
        [ReadOnly] public GlyphTable glyphTable;
        [ReadOnly] public ComponentLookup<FontAssetMetadata> fontAssetMetadataLookup; //temporary link between Font Entities and FontTable

        public BufferLookup<MissingGlyphs> missingGlyphsBuffer;
        public BufferLookup<UsedGlyphs> usedGlyphsBuffer;
        public BufferLookup<UsedGlyphRects> usedGlyphRectsBuffer;
        public BufferLookup<FreeGlyphRects> freeGlyphRectsBuffer;

        [NativeSetThreadIndex]
        int threadIndex;

        public void Execute()
        {
            var atlasData = atlasDataLookup[fontEntity];
            var missingGlyphs = missingGlyphsBuffer[fontEntity];
            var usedGlyphs = usedGlyphsBuffer[fontEntity].Reinterpret<uint>();
            var usedGlyphRects = usedGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();
            var freeGlyphRects = freeGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();

            var fontAssetMetaData = fontAssetMetadataLookup[fontEntity];
            var face = fontTable.faces[fontAssetMetaData.faceIndex];
            var font = fontTable.GetOrCreateFont(fontAssetMetaData.faceIndex, threadIndex);
            var samplingSize = FontTextureSize.Normal.GetSamplingSize();
            font.SetScale(samplingSize, samplingSize);

            var glyphsToPlace = new NativeList<GlyphBlob>(256, Allocator.Temp);

            for (int i = 0, ii = missingGlyphs.Length; i < ii; i++)
            {
                var glyphID = glyphTable.glyphHashToIdMap[missingGlyphs[i].key];
                var glyphEntry = glyphTable.GetEntry(glyphID);

                var hbGlyph = new GlyphBlob { entry=glyphEntry };
                glyphsToPlace.Add(hbGlyph);
            };
            var success = NativeAtlas.AddGlyphs(atlasData.padding, glyphsToPlace, placedGlyphs, usedGlyphs, usedGlyphRects, freeGlyphRects);
            if (!success)
            {
                Debug.Log($"{glyphsToPlace.Length} glyphs could not be placed for font {fontAssetMetaData.family} {fontAssetMetaData.subfamily} ");
                //for (int i = 0, ii = usedGlyphs.Length; i < ii; i++)
                //{
                //    var glyphExtents= glyphsToPlace[i].glyphExtents;
                //    Debug.Log($"Glyph Rect: {glyphExtents.width} {glyphExtents.height} padding: {atlasData.padding}");
                //}
                //for (int i = 0, ii = freeGlyphRects.Length; i < ii; i++)
                //{
                //    var freeGlyphRect = freeGlyphRects[i];
                //    Debug.Log($"Free Rect: {freeGlyphRect.width} {freeGlyphRect.height}");
                //}
                //missingGlyphs.Clear();
                //for (int i = 0, ii = usedGlyphs.Length; i < ii; i++)
                //    missingGlyphs.Add(glyphsToPlace[i].glyphID);
                //return;
            }
            missingGlyphs.Clear();
        }
    }    
}
