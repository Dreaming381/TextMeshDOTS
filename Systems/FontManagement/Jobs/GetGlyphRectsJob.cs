using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using TextMeshDOTS;
using UnityEngine.TextCore;


namespace HarfBuzz.SDF
{
    [BurstCompile]
    struct GetGlyphRectsJob : IJob
    {
        public NativeList<GlyphBlob> placedGlyphs;

        public Entity fontEntity;

        [ReadOnly] public ComponentLookup<HBFontAssetRef> hbFontAssetRefLookup;
        [ReadOnly] public ComponentLookup<HBFontPointer> hbFontPointerLookup;

        public BufferLookup<HBMissingGlyphs> missingGlyphsBuffer;
        public BufferLookup<HBUsedGlyphs> usedGlyphsBuffer;
        public BufferLookup<HBUsedGlyphRects> usedGlyphRectsBuffer;
        public BufferLookup<HBFreeGlyphRects> freeGlyphRectsBuffer;

        public void Execute()
        {
            var hbFontAsset = hbFontAssetRefLookup[fontEntity];
            var hbFontPointer = hbFontPointerLookup[fontEntity];
            var missingGlyphs = missingGlyphsBuffer[fontEntity].Reinterpret<uint>();
            var usedGlyphs = usedGlyphsBuffer[fontEntity].Reinterpret<uint>();
            var usedGlyphRects = usedGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();
            var freeGlyphRects = freeGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();


            var font = hbFontPointer.font;
            var glyphBlobs = new NativeList<GlyphBlob>(256, Allocator.Temp);

            for (int i = 0, ii = missingGlyphs.Length; i < ii; i++)
            {
                var glyphID = missingGlyphs[i];
                font.GetGlyphExtends(glyphID, out GlyphExtents extends);

                extends.height = -extends.height; //y-axis in harfbuzz is top to bottom (positve values are down), but this library assumes bottom to top (positve values are up)
                var hbGlyph = new GlyphBlob { glyphID = glyphID, glyphExtents = extends};
                glyphBlobs.Add(hbGlyph);
            };
            NativeAtlas.AddGlyphs(hbFontAsset.padding, glyphBlobs, placedGlyphs, usedGlyphs, usedGlyphRects, freeGlyphRects);
            missingGlyphs.Clear();
        }
    }    
}
