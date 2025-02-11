using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore;
using TextMeshDOTS.HarfBuzz.Bitmap;
using UnityEngine;


namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct GetGlyphRectsJob : IJob
    {
        public NativeList<GlyphBlob> placedGlyphs;

        public Entity fontEntity;

        [ReadOnly] public ComponentLookup<AtlasData> atlasDataLookup;
        [ReadOnly] public ComponentLookup<NativeFontPointer> nativeFontPointerLookup;
        [ReadOnly] public ComponentLookup<FontAssetMetadata> fontAssetMetadataLookup;

        public BufferLookup<MissingGlyphs> missingGlyphsBuffer;
        public BufferLookup<UsedGlyphs> usedGlyphsBuffer;
        public BufferLookup<UsedGlyphRects> usedGlyphRectsBuffer;
        public BufferLookup<FreeGlyphRects> freeGlyphRectsBuffer;

        public void Execute()
        {
            var atlasData = atlasDataLookup[fontEntity];
            var nativeFontPointer = nativeFontPointerLookup[fontEntity];
            var missingGlyphs = missingGlyphsBuffer[fontEntity].Reinterpret<uint>();
            var usedGlyphs = usedGlyphsBuffer[fontEntity].Reinterpret<uint>();
            var usedGlyphRects = usedGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();
            var freeGlyphRects = freeGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();

            var font = nativeFontPointer.font;
            var glyphsToPlace = new NativeList<GlyphBlob>(256, Allocator.Temp);

            for (int i = 0, ii = missingGlyphs.Length; i < ii; i++)
            {
                var glyphID = missingGlyphs[i];
                //GetGlyphExtends is a very costly function. For a COLR glyph, rect is determined by parsing all vertices of maybe 20 sub-glyphs
                //calling it in parallel makes things worse (mutex lock?)
                font.GetGlyphExtents(glyphID, out GlyphExtents extends);                
                var hbGlyph = new GlyphBlob { glyphID = glyphID, glyphExtents = extends};
                glyphsToPlace.Add(hbGlyph);
            };
            var success = NativeAtlas.AddGlyphs(atlasData.padding, glyphsToPlace, placedGlyphs, usedGlyphs, usedGlyphRects, freeGlyphRects);
            if (!success)
            {
                var fontAssetMetaData = fontAssetMetadataLookup[fontEntity];
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
