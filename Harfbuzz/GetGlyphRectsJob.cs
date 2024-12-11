using HarfBuzz.SDF;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using System;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;


namespace HarfBuzz.SDF
{
    [BurstCompile]
    struct GetGlyphRectsJob : IJob
    {
        public NativeList<uint> reservedGlyphIDs;
        public NativeHashMap<uint, RectInt> usedRects;
        public NativeList<RectInt> freeRects;

        [ReadOnly] public NativeList<uint> glyphIDs;
        [NativeDisableUnsafePtrRestriction] [ReadOnly] public Font font;
        [NativeDisableUnsafePtrRestriction] [ReadOnly] public IntPtr drawFunct;
        public void Execute()
        {
            var hbGlyphs = new NativeList<HBGlyph>(256, Allocator.Temp);
            var bezierData = new BezierData(256, 16, Allocator.Temp);
            for (int i = 0, ii = glyphIDs.Length; i < ii; i++)
            {
                var glyphID= glyphIDs[i];
                HB.hb_font_draw_glyph(font.ptr, glyphID, drawFunct, ref bezierData);
                bezierData.contourIDs.Add(bezierData.edges.Length);//close the last contour
                if(!bezierData.glyphRect.IsValid)
                {
                    Debug.Log($"Ignoring glyph ID {glyphID} because it has no size");
                    continue;
                }
                bezierData.glyphRect.Expand(9);
                var edges = bezierData.edges;
                var shift = -bezierData.glyphRect.min;
                for (int k = 0, kk = edges.Length; k < kk; k++)
                {
                    ref var edge = ref edges.ElementAt(k);
                    edge.start_pos += shift;
                    edge.end_pos += shift;
                    edge.control1 += shift;
                    edge.control2 += shift;
                }
                bezierData.glyphRect.min = bezierData.glyphRect.min + shift;
                bezierData.glyphRect.max = bezierData.glyphRect.max + shift;
                var bbox = bezierData.glyphRect;
                var hbGlyph = new HBGlyph { glyphID = glyphID, glyphRect = new RectInt((int)bbox.min.x, (int)bbox.min.y, (int)bbox.width, (int)bbox.height) };
                hbGlyphs.Add(hbGlyph);
                //if(NativeAtlas.TryAddGlyph(hbGlyph, freeRects, usedRects, out _))
                //    reservedGlyphIDs.Add(glyphID);
                bezierData.Clear();
            };
            NativeAtlas.AddGlyphs(hbGlyphs, reservedGlyphIDs, freeRects, usedRects);
        }
    }
}
