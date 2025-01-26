using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TextCore;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{
    public static class SDF_SPMD
    {
        //generates SDF directly from Bezier Data that are provided by Harfbuzz
        //approach is inspired by FreeType 
        public static SignedDistance4 maxSDF => new SignedDistance4 { distance = int.MaxValue, sign = 0, cross = 0 };


        /// <summary>
        /// Converts a glyph into a SDF bitmap. When using this function, ensure all bezier edges have been split into line edges first 
        /// </summary>
        public static bool SDFGenerateSubDivisionLineEdges(SDFOrientation orientation, ref DrawData drawData, NativeArray<byte> buffer, GlyphRect glyphRect, int atlasWidth, int atlasHeight, int spread = SDFCommon.DEFAULT_SPREAD)
        {
            if (drawData.contourIDs.Length < 2 || drawData.edges.Length == 0)
                return false;

            return SDFGenerateBoundingBoxLineEdges(ref drawData, orientation, spread, buffer, glyphRect, atlasWidth, atlasHeight);
        }

        static bool SDFGenerateBoundingBoxLineEdges(ref DrawData drawData, SDFOrientation orientation, int spread, NativeArray<byte> buffer, GlyphRect glyphRect, int atlasWidth, int atlasHeight)
        {
            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;

            bool flip_y = true;
            bool flip_sign = false;
            int outsideSign = -1;
            float sp_sq;
            SDFEdge edge;
            var dists = new NativeArray<SignedDistance>(glyphRect.width * glyphRect.height, Allocator.Temp);

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;

            if (SDFCommon.USE_SQUARED_DISTANCES)
                sp_sq = spread * spread;
            else
                sp_sq = spread;

            var rectX = glyphRect.x;
            var rectY = glyphRect.y;
            var rectWidth = glyphRect.width;
            var rectHeight = glyphRect.height;

            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    edge = edges[edgeID];
                    var cbox = BezierMath.GetLineBBox(edge.start_pos, edge.end_pos);
                    cbox.Expand(spread);
                    float4 ax = edge.start_pos.x;
                    float4 ay = edge.start_pos.y;
                    float4 bx = edge.end_pos.x;
                    float4 by = edge.end_pos.y;
                    float4 gridPointx=default;
                    float4 gridPointy;
                    SignedDistance4 dist = maxSDF;

                    /* now loop over the pixels in the control box. */
                    for (int y = math.max((int)cbox.min.y, 0), yEnd = math.min((int)cbox.max.y, rectHeight); y < yEnd; y++)
                    {                        
                        gridPointy = y + 0.5f;     // use the center of any pixel to be rendered within cbox
                        //xEnd +3 because of 4 pixel stride (ensure we process also last 3 pixel)
                        for (int x = math.max((int)cbox.min.x,0), xEnd = math.min((int)cbox.max.x, rectWidth); x < xEnd +3; x = x + 4) 
                        {                            
                            var x4 = new int4(x+0, x+1, x+2, x+3);                            
                            gridPointx = x4;
                            gridPointx += 0.5f; // use the center of any pixel to be rendered within cbox
                            GetMinDistanceLineToPoint(ax, ay, bx, by, gridPointx, gridPointy, ref dist);

                            dist.sign = math.select(dist.sign, -dist.sign, orientation == SDFOrientation.FILL_LEFT);

                            // ignore if the distance is greater than spread;
                            // otherwise it creates artifacts due to the wrong sign
                            var validDist = dist.distance <= sp_sq;
                            dist.distance = math.select(dist.distance, math.sqrt(dist.distance), SDFCommon.USE_SQUARED_DISTANCES);
                            var indices = math.select((int4)((rectHeight - y - 1) * rectWidth) + x4, (y * rectWidth) + x4, flip_y);

                            var validX = x4 < rectWidth;
                            validX = validX & validDist;
                            for (int i = 0; i < 4; i++)
                            {
                                if (!validX[i])
                                    continue;

                                var index = indices[i];
                                var targetDist = dists[index];
                                if (targetDist.sign == 0) // check if the pixel is already set
                                    targetDist = dist[i];
                                else
                                {
                                    if (BezierMath.EqualsForLargeValues(targetDist.distance, dist.distance[i]))
                                        targetDist = ResolveCorner(ref targetDist, ref dist, i);
                                    else if (targetDist.distance > dist.distance[i])
                                        targetDist = dist[i];
                                }
                                dists[index] = targetDist;
                            }
                        }
                    }
                }
            }

            // final pass
            for (int row = 0; row < rectHeight; row++)
            {
                /* We assume the starting pixel of each row is outside. */
                int current_sign = outsideSign;

                for (int column = 0; column < rectWidth; column++)
                {
                    var sourceIndex = rectWidth * row + column;
                    var targetIndex = (atlasWidth * (row + rectY)) + (column + rectX);

                    // if the pixel is not set, its shortest distance is more than `spread`
                    var dist = dists[sourceIndex];
                    if (BezierMath.EqualsForSmallValues(dist.sign, 0))
                    {
                        dist.sign = outsideSign;
                        dist.distance = -spread;
                    }
                    else
                        current_sign = dist.sign;

                    dist.distance = math.clamp(dist.distance, -spread, spread);

                    // flip sign if required
                    dist.distance *= flip_sign ? -current_sign : current_sign;
                    dists[sourceIndex] = dist;

                    // convert to byte range of alpha8 texture
                    var result = ((dist.distance + spread) * 16);
                    buffer[targetIndex] = (byte)result;
                }
            }
            return true;
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool GetMinDistanceLineToPoint(float4 ax, float4 ay, float4 bx, float4 by, float4 px, float4 py, ref SignedDistance4 dist)
        {
            var abx = bx - ax;                          // Vector from A to B
            var aby = by - ay;                          // Vector from A to B
            var apx = px - ax;                          // Vector from A to P
            var apy = py - ay;                          // Vector from A to P
            var abLengthSq = abx*abx + aby*aby;
            var frac = abx*apx + aby*apy;
            frac = math.max(frac, 0.0f);                // Check if P projection is over vectorAB 
            frac = math.min(frac, abLengthSq);          // Check if P projection is over vectorAB 

            frac = frac / abLengthSq;                   // The normalized "distance" from a to your closest point
            var nx = ax + abx * frac;                   // nearest point on egde
            var ny = ay + aby * frac;                   // nearest point on egde

            var pnx = nx - px;
            var pny = ny - py;
            var pnLengthSq = pnx * pnx + pny * pny;
            var cross = BezierMath.cross2D(pnx, pny, abx, aby);

            dist.sign = math.select(-1,1, cross < 0);
            dist.distance = math.select( math.sqrt(pnLengthSq), pnLengthSq, SDFCommon.USE_SQUARED_DISTANCES);

            var nIsEndPoint = BezierMath.EqualsForSmallValues(frac, 0) | BezierMath.EqualsForSmallValues(frac, 1);
            dist.cross = math.select(1, GetCross(abx, aby, pnx, pny, abLengthSq, pnLengthSq), nIsEndPoint);

            return true;
        }
        static float4  GetCross(float4 abx, float4 aby, float4 pnx, float4 pny, float4 abLengthSq, float4 pnLengthSq)
        {
            var abLength = math.sqrt(abLengthSq);
            var pnLength = math.sqrt(pnLengthSq);
            var abxNorm = abx / abLength;
            var abyNorm = aby / abLength;
            var pnxNorm = pnx / pnLength;
            var pnyNorm = pny / pnLength;
            return BezierMath.cross2D(abxNorm, abyNorm, pnxNorm, pnyNorm);
        }
        public static SignedDistance ResolveCorner(ref SignedDistance sdf1, ref SignedDistance4 sdfs, int index)
        {
            var sdf2 = sdfs[index];
            return math.abs(sdf1.cross) > math.abs(sdf2.cross) ? sdf1 : sdf2;
        }
        public static void ResolveCorner(float4 dist1, float4 cross1, int4 sign1,
                                         float4 dist2, float4 cross2, int4 sign2,
                                         out float4 dist, out float4 cross, out int4 sign)
        {
            var condition = math.abs(cross1) > math.abs(cross2);
            dist = math.select(dist2, dist1, condition);
            cross = math.select(cross2, cross1, condition);
            sign = math.select(sign2, sign1, condition);
        }
    }
}