using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using TextMeshDOTS;
using Codice.Client.BaseCommands;
using Unity.Mathematics;
using UnityEngine.TextCore;


namespace HarfBuzz.SDF
{
    [BurstCompile]
    struct GetGlyphRectsJob : IJob
    {
        public Entity fontEntity;
        [ReadOnly] public ComponentLookup<HBFontPointer> hbFontPointerLookup;

        public NativeList<GlyphBlob> placedGlyphs;
        public NativeHashMap<uint, GlyphRect> usedRects;
        public NativeList<GlyphRect> freeRects;

        public int padding;
        //public NativeList<uint> glyphIDs;
        public DynamicBuffer<uint> glyphIDs;

        public void Execute()
        {
            var hbFontPointer = hbFontPointerLookup[fontEntity];
            var font = hbFontPointer.font;
            var glyphBlobs = new NativeList<GlyphBlob>(256, Allocator.Temp);
            var doublePadding = 2 * padding;
            for (int i = 0, ii = glyphIDs.Length; i < ii; i++)
            {
                var glyphID = glyphIDs[i];
                font.GetGlyphExtends(glyphID, out GlyphExtents extends);
                //if (extends.width == 0)
                //{
                //    Debug.Log($"Ignoring glyph ID {glyphID}  {extends} because it has no size");
                //    var emptyGlyph=new GlyphBlob { glyphID = glyphID, glyphExtents = extends, glyphRect = new GlyphRect() };
                //    placedGlyphs.Add(emptyGlyph);
                //    continue;
                //}
                extends.height = -extends.height; //y-axis in harfbuzz is top to bottom (positve values are down), but this library assumes bottom to top (positve values are up)
                var hbGlyph = new GlyphBlob { glyphID = glyphID, glyphExtents = extends};
                glyphBlobs.Add(hbGlyph);
            };
            NativeAtlas.AddGlyphs(padding, glyphBlobs, placedGlyphs, usedRects, freeRects);
            glyphIDs.Clear();
        }
    }
}
