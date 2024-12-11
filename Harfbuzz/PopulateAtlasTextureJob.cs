using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;


namespace HarfBuzz.SDF
{
    [BurstCompile]
    struct PopulateAtlasTextureJob : IJobParallelForDefer
    {
        [NativeDisableParallelForRestriction] public NativeArray<byte> textureData;

        public int atlasWidth;
        public int atlasHeight;
        [ReadOnly] public NativeList<uint> reservedGlyphIDs;
        [ReadOnly] public NativeHashMap<uint, RectInt> usedRects;
        [NativeDisableUnsafePtrRestriction][ReadOnly] public Font font;
        [NativeDisableUnsafePtrRestriction][ReadOnly] public IntPtr drawFunct;
        public ProfilerMarker marker;
        public void Execute(int i)
        {
            var bezierData = new BezierData(256, 16, Allocator.Temp);
            var glyphID = reservedGlyphIDs[i];
            HB.hb_font_draw_glyph(font.ptr, glyphID, drawFunct, ref bezierData);
            bezierData.contourIDs.Add(bezierData.edges.Length);//close the last contour
            marker.Begin();
            if (usedRects.TryGetValue(glyphID, out var bestRect))
                SDF.SDFGenerateSubDivision(ref bezierData, SDFCommon.DEFAULT_SPREAD, textureData, bestRect, atlasWidth, atlasHeight);

            //SDFFixedPoint.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, textureData, atlasWidth, atlasHeight);
            //SDF.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, textureData, atlasWidth, atlasHeight);
             marker.End();
        }
    }    
}