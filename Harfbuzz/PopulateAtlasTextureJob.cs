using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using TextMeshDOTS;
using Unity.Entities;
using UnityEngine.TextCore;
using Unity.Mathematics;


namespace HarfBuzz.SDF
{
    [BurstCompile]
    struct PopulateAtlasTextureJob : IJobParallelForDefer
    {
        public int padding;
        public Entity fontEntity;
        [ReadOnly] public ComponentLookup<HBFontPointer> hbFontPointerLookup;
        [NativeDisableParallelForRestriction] public NativeArray<byte> textureData;

        public int atlasWidth;
        public int atlasHeight;
        [ReadOnly] public NativeList<GlyphBlob> placedGlyphs;
        [ReadOnly] public NativeHashMap<uint, GlyphRect> usedRects;

        public ProfilerMarker marker;
        public void Execute(int i)
        {
            var glyphBlob = placedGlyphs[i];
            //if (glyphBlob.glyphExtents.width == 0)//emtpy glyph, no need to add to texture.
            //    return;

            var hbFontPointer = hbFontPointerLookup[fontEntity];
            var font = hbFontPointer.font;
            var drawFunct = hbFontPointer.hbDrawFuncts;

            var bezierData = new BezierData(256, 16, Allocator.Temp);
            
            HB.hb_font_draw_glyph(font.ptr, glyphBlob.glyphID, drawFunct, ref bezierData);
            bezierData.contourIDs.Add(bezierData.edges.Length);//close the last contour

            var edges = bezierData.edges;

            //shift the bezier edges so that they are in the center of the reserved atlas padded texture window (usedRects)
            var shiftx = bezierData.glyphRect.min.x - ((glyphBlob.glyphExtents.width + 2 * padding - bezierData.glyphRect.width) / 2);
            var shifty = bezierData.glyphRect.min.y - ((glyphBlob.glyphExtents.height + 2 * padding - bezierData.glyphRect.height) / 2);
            float2 shift = -new float2(shiftx, shifty);
            for (int k = 0, kk = edges.Length; k < kk; k++)
            {
                ref var edge = ref edges.ElementAt(k);
                edge.start_pos += shift;
                edge.end_pos += shift;
                edge.control1 += shift;
                edge.control2 += shift;
                //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
            }

            marker.Begin();
            var atlasRect = usedRects[glyphBlob.glyphID];//render SDF into the reserved padded atlas texture  window 
            SDF.SDFGenerateSubDivision(ref bezierData, SDFCommon.DEFAULT_SPREAD, textureData, atlasRect, atlasWidth, atlasHeight);
            //SDFFixedPoint.SDFGenerateSubDivision(ref bezierData, SDFCommon.DEFAULT_SPREAD, textureData, atlasRect, atlasWidth, atlasHeight);
            //SDFFixedPoint.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, textureData, atlasWidth, atlasHeight);
            //SDF.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, textureData, atlasWidth, atlasHeight);

             marker.End();
        }
    }    
}