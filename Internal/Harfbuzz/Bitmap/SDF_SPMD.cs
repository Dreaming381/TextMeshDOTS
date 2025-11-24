using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TextCore;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{
    [BurstCompile]
    internal static class SDF_SPMD
    {
        //generates SDF directly from bezier curves provided by Harfbuzz. This version actually expects quadratic and
        //cubic bezier curves to be flattened to lines. Distance to lines is much less math compared to distance to bezier curves, so this approach is faster overall.
        //approach is inspired by FreeType 

        /// <summary>
        /// Converts a glyph into a SDF bitmap. When using this function, ensure all bezier edges have been split into line edges first. Distance to lines is much less 
        /// math compared to distance to bezier curves, so this approach is faster overall.
        /// </summary>
        [BurstCompile]
        public static bool SDFGenerateSubDivisionLineEdges(SDFOrientation orientation, ref DrawData drawData, ref NativeArray<byte> buffer, ref GlyphRect atlasRect, int padding, int atlasWidth, int atlasHeight, int spread = SDFCommon.DEFAULT_SPREAD)
        {
            if (drawData.contourIDs.Length < 2 || drawData.edges.Length == 0)
                return false;

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;            

            bool flip_y = true;
            bool flip_sign = false;
            var offset = drawData.glyphRect.min - padding;            
            var atlasRectWidth = atlasRect.width;
            var atlasRectHeight = atlasRect.height;
            
            float sp_sq = math.select(spread, spread * spread, SDFCommon.USE_SQUARED_DISTANCES);

            var size = atlasRectWidth * atlasRectHeight;
            var targetDistances = new NativeArray<float>(size, Allocator.Temp);
            var targetCrosses = new NativeArray<float>(size, Allocator.Temp);
            var targetSigns = new NativeArray<int>(size, Allocator.Temp);

            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                var startID = contourIDs[contourID];
                var nextStartID = contourIDs[contourID + 1];
                GetDistancesForContour(edges, startID, nextStartID, targetDistances, targetCrosses, targetSigns, orientation, atlasRectHeight, atlasRectWidth, spread, sp_sq, flip_y, offset);                
            }
            if (flip_sign)
                SDFCommon.FinalPassFlipSign(targetDistances, targetCrosses, targetSigns, spread, atlasRect, atlasWidth, atlasHeight);
            else
                SDFCommon.FinalPass(targetDistances, targetCrosses, targetSigns, spread, atlasRect, atlasWidth, atlasHeight);

            SDFCommon.GetAlphaTexture(targetDistances, buffer, spread, atlasRect, atlasWidth, atlasHeight);
            return true;
        }

        [BurstCompile]
        public static bool SDFGenerateSubDivisionLineEdges_Overlap(SDFOrientation orientation, ref DrawData drawData, NativeArray<byte> buffer, GlyphRect atlasRect, int padding, int atlasWidth, int atlasHeight, int spread = SDFCommon.DEFAULT_SPREAD)
        {
            if (drawData.contourIDs.Length < 2 || drawData.edges.Length == 0)
                return false;

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;

            bool flip_y = true;
            bool flip_sign = false;
            var offset = drawData.glyphRect.min - padding;
            var atlasRectWidth = atlasRect.width;
            var atlasRectHeight = atlasRect.height;

            float sp_sq = math.select(spread, spread * spread, SDFCommon.USE_SQUARED_DISTANCES);

            var size = atlasRectWidth * atlasRectHeight;
            var targetDistances = new NativeArray<float>(size, Allocator.Temp);
            var targetCrosses = new NativeArray<float>(size, Allocator.Temp);
            var targetSigns = new NativeArray<int>(size, Allocator.Temp);

            var tempDistances = new NativeArray<float>(size, Allocator.Temp);
            var tempCrosses = new NativeArray<float>(size, Allocator.Temp);
            var tempSigns = new NativeArray<int>(size, Allocator.Temp);

            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;

            //get bitmap for first contour
            int startID = contourIDs[0];
            int nextStartID = contourIDs[0 + 1]; 
            GetDistancesForContour(edges, startID, nextStartID, targetDistances, targetCrosses, targetSigns, orientation, atlasRectHeight, atlasRectWidth, spread, sp_sq, flip_y, offset);
            if (flip_sign)
                SDFCommon.FinalPassFlipSign(targetDistances, targetCrosses, targetSigns, spread, atlasRect, atlasWidth, atlasHeight);
            else
                SDFCommon.FinalPass(targetDistances, targetCrosses, targetSigns, spread, atlasRect, atlasWidth, atlasHeight);

            for (int contourID = 1, end = contourIDs.Length - 1; contourID < end; contourID++) //for each remaining contour
            {
                startID = contourIDs[contourID];
                nextStartID = contourIDs[contourID + 1];
                GetDistancesForContour(edges, startID, nextStartID, tempDistances, tempCrosses, tempSigns, orientation, atlasRectHeight, atlasRectWidth, spread, sp_sq, flip_y, offset);
                if (flip_sign)
                    SDFCommon.FinalPassFlipSign(tempDistances, tempCrosses, tempSigns, spread, atlasRect, atlasWidth, atlasHeight);
                else
                    SDFCommon.FinalPass(tempDistances, tempCrosses, tempSigns, spread, atlasRect, atlasWidth, atlasHeight);

                var contourOrientation = GetPolyOrientation(SignedArea(edges, startID, nextStartID));
                SDFCommon.MergeSDF(targetDistances, targetCrosses, targetSigns, tempDistances, tempCrosses, tempSigns, sp_sq, contourOrientation);
                tempDistances.ClearArray();
                tempCrosses.ClearArray();
                tempSigns.ClearArray();
            }
            SDFCommon.GetAlphaTexture(targetDistances, buffer, spread, atlasRect, atlasWidth, atlasHeight);
            return true;
        }       

        static void GetDistancesForContour(
            NativeList<SDFEdge> edges, 
            int startID,
            int nextStartID,
            NativeArray<float> targetDistances,
            NativeArray<float> targetCrosses,
            NativeArray<int> targetSigns,
            SDFOrientation orientation, 
            int atlasRectHeight,
            int atlasRectWidth,
            int spread,
            float sp_sq,
            bool flip_y,
            float2 offset
            )
        {            
            for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
            {
                var edge = edges[edgeID];
                var p0 = edge.start_pos - offset;
                var p1 = edge.end_pos - offset;
                var cbox = BezierMath.GetLineBBox(p0, p1);
                cbox.Expand(spread);

                /* now loop over the pixels in the control box. */
                int yEnd = math.min((int)cbox.max.y, atlasRectHeight);
                int xStartx = math.max((int)cbox.min.x, 0);
                int xEnd = math.min((int)cbox.max.x, atlasRectWidth);
                int xLength = xEnd - xStartx;
                int xEndSIMD = xEnd - (xLength % 4);
                for (int y = math.max((int)cbox.min.y, 0); y < yEnd; y++)
                {
                    float4 gridPointy = y + 0.5f;     // use the center of any pixel to be rendered within cbox
                    int x = xStartx;
                    float4 ax = p0.x;
                    float4 ay = p0.y;
                    float4 bx = p1.x;
                    float4 by = p1.y;

                    //process vectorized
                    for (; x < xEndSIMD; x += 4)
                    {
                        float4 gridPointx = new int4(x + 0, x + 1, x + 2, x + 3);
                        gridPointx += 0.5f; // use the center of any pixel to be rendered within cbox
                        SDFCommon.GetMinDistanceLineToPoint(ax, ay, bx, by, gridPointx, gridPointy, out float4 distance, out float4 cross, out int4 sign);

                        sign = math.select(sign, -sign, orientation == SDFOrientation.FILL_LEFT);
                        distance = math.select(distance, math.sqrt(distance), SDFCommon.USE_SQUARED_DISTANCES);

                        var index = math.select(((atlasRectHeight - y - 1) * atlasRectWidth) + x, (y * atlasRectWidth) + x, flip_y);
                        SDFCommon.GetTarget_DistanceCrossSign(targetDistances, targetCrosses, targetSigns, index, out float4 targetDistance, out float4 targetCross, out int4 targetSign);
                        SDFCommon.GetValid_DistanceCrossSign(ref distance, ref cross, ref sign, ref targetDistance, ref targetCross, ref targetSign, sp_sq, out var validDistance, out var validCross, out var validSign);
                        SDFCommon.SetTarget_DistanceCrossSign(targetDistances, targetCrosses, targetSigns, index, ref validDistance, ref validCross, ref validSign);
                    }
                    x -= 3;//next index: wind back -4, add 1

                    //process remainder
                    float gridPointy_float = y + 0.5f;     // use the center of any pixel to be rendered within cbox
                    for (; x < xEnd; x++)
                    {
                        float gridPointx = x;
                        gridPointx += 0.5f; // use the center of any pixel to be rendered within cbox
                        SDFCommon.GetMinDistanceLineToPoint(p0.x, p0.y, p1.x, p1.y, gridPointx, gridPointy_float, out float distance, out float cross, out int sign);

                        sign = math.select(sign, -sign, orientation == SDFOrientation.FILL_LEFT);
                        distance = math.select(distance, math.sqrt(distance), SDFCommon.USE_SQUARED_DISTANCES);

                        var index = math.select(((atlasRectHeight - y - 1) * atlasRectWidth) + x, (y * atlasRectWidth) + x, flip_y);
                        SDFCommon.GetTarget_DistanceCrossSign(targetDistances, targetCrosses, targetSigns, index, out float targetDistance, out float targetCross, out int targetSign);
                        SDFCommon.GetValid_DistanceCrossSign(ref distance, ref cross, ref sign, ref targetDistance, ref targetCross, ref targetSign, sp_sq, out var validDistance, out var validCross, out var validSign);
                        SDFCommon.SetTarget_DistanceCrossSign(targetDistances, targetCrosses, targetSigns, index, ref validDistance, ref validCross, ref validSign);
                    }
                }
            }
        }        

        /// /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedArea(NativeList<SDFEdge> data, int start, int end)
        {
            float area = default;
            for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
                area += (data[prev].start_pos.x - data[i].start_pos.x) * (data[i].start_pos.y + data[prev].start_pos.y);
            return area * 0.5f;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolyOrientation GetPolyOrientation(double signedArea)
        {
            if (signedArea < 0)
                return PolyOrientation.CW;
            else if (signedArea > 0)
                return PolyOrientation.CCW;
            else
                return PolyOrientation.None;
        }
        public enum PolyOrientation : byte
        {
            CW = 0,
            CCW = 1,
            None = 2,
        }
    }
}