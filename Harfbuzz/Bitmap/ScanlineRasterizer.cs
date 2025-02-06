using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{
    public static class ScanlineRasterizer
    {
        public static void Rasterize<T>(ref DrawData drawData, NativeArray<ColorARGB> textureData, T pattern, BBox clipRect, bool inverse = false) where T: IPattern
        {
            //Debug.Log("Rasterize");
            var intersectionPoints = new NativeList<float2>(256, Allocator.Temp);

            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var step = 1;

            var minX = clipRect.min.x;
            var maxX = clipRect.max.x;
            var minY = clipRect.min.y;
            var maxY = clipRect.max.y;

            var glyphRect = drawData.glyphRect;
            //var minX = glyphRect.min.x-10;
            //var maxX = glyphRect.max.x+10;
            //var minY = glyphRect.min.y-10;
            //var maxY = glyphRect.max.y+10;

            var clipRectMinX = (int)clipRect.min.x;            
            var clipRectMinY = (int)clipRect.min.y;
            var clipRectMaxX = (int)clipRect.max.x;
            var width = clipRect.intWidth;
            //var width = 1024;

            var scanLineStart = new float2(minX, minY);
            var scanLineEnd = new float2(maxX, minY);

            //for (float y = minY; y < maxY; y += step)
            //for (float y = 155; y < 170; y += step)
            for (float y = 10; y < 195; y += step)
            {
                scanLineStart.y = y; scanLineEnd.y = y;
                intersectionPoints.Clear();
                if (inverse)
                    intersectionPoints.Add(new float2(clipRectMinX, y));

                for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
                {
                    int startID = contourIDs[contourID];
                    int nextStartID = contourIDs[contourID + 1];
                    for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                    {
                        var edge = edges[edgeID];
                        //if (edge.edge_type == SDFEdgeType.QUADRATIC)
                        //    IntersectQuadraticBezierAndScanline(edges, edgeID, startID, nextStartID, (int)y, (int) minX, (int)maxX, intersectionPoints);
                        //else
                        {
                            bool intersect = EdgesIntersect(scanLineStart, scanLineEnd, edge.start_pos, edge.end_pos, true);
                            if (intersect)
                            {
                                GetIntersectPt(scanLineStart, scanLineEnd, edge.start_pos, edge.end_pos, out float2 intersectPoint);
                                if (IntersectionAtExposedVertex(edge.start_pos.y == y, edge.end_pos.y == y, edgeID, startID, nextStartID, edges, new float2(minX, y), intersectPoint))
                                    continue;
                                
                                if (intersectionPoints.Contains(intersectPoint) && DoNotDoublicateIntersectionPoint(edge.start_pos.y == y, edge.end_pos.y == y, edgeID, startID, nextStartID, edges))
                                    continue;                                
                                intersectionPoints.Add(intersectPoint);
                            }
                        }
                    }
                }
                if (inverse)
                    intersectionPoints.Add(new float2(clipRectMaxX, y));

                intersectionPoints.Sort(default(XComparer));                
               

                for (int i = 0; i < intersectionPoints.Length - 1; i += 2)
                {                    
                    var startX = (int)intersectionPoints[i].x;
                    var endX = (int)intersectionPoints[i + 1].x;

                    for (int column = startX; column < endX; column += step)
                    {
                        var color = pattern.GetColor(new float2(column, y));
                        var targetIndex = width * ((int)y - clipRectMinY) + column - clipRectMinX; //substracting clipRect.min results in aliging glyph with (0,0) of bitmap

                        var colorDest = textureData[targetIndex];
                        textureData[targetIndex] = color;
                        //textureData[targetIndex] = Blending.SrcOver(color, colorDest);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a positive value if the points a, b, and c occur in counterclockwise order (CCW, c lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(CW, c lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// result also happens to be twice the signed area of the triangle
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Orient2DFast(double2 a, double2 b, double2 p)
        {
            return (a.x - p.x) * (b.y - p.y) - (a.y - p.y) * (b.x - p.x);
        }
        //source: clipper2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EdgesIntersect(double2 a1, double2 a2, double2 b1, double2 b2, bool inclusive = false)
        {
            if (inclusive)
            {
                double res1 = Orient2DFast(a1, b1, b2);
                double res2 = Orient2DFast(a2, b1, b2);
                if (res1 * res2 > 0) return false;//a1 and a2 are on same side of edge b-->cannot intersect
                double res3 = Orient2DFast(b1, a1, a2);
                double res4 = Orient2DFast(b2, a1, a2);
                if (res3 * res4 > 0) return false;//b1 and b2 are on same side of edge a-->cannot intersect

                // ensure NOT collinear =only report "no intersection" when all points are colinear
                //when one point of any edge is not 0, there is an intersection
                return (res1 != 0 || res2 != 0 || res3 != 0 || res4 != 0);
            }
            else
            {
                double res1 = Orient2DFast(a1, b1, b2);
                double res2 = Orient2DFast(a2, b1, b2);
                double res3 = Orient2DFast(b1, a1, a2);
                double res4 = Orient2DFast(b2, a1, a2);
                //reports intersection only when edge points are on opposite site of the other edge
                //when one point is ON the other edge (=touching), no intersection
                return (res1 * res2 < 0) && (res3 * res4 < 0);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GetIntersectPt(float2 a1, float2 a2, float2 b1, float2 b2, out float2 ip)
        {
            float dy1 = (a2.y - a1.y);
            float dx1 = (a2.x - a1.x);
            float dy2 = (b2.y - b1.y);
            float dx2 = (b2.x - b1.x);
            float det = dy1 * dx2 - dy2 * dx1;
            if (det == 0.0)
            {
                ip = new float2();
                return false;
            }

            float t = ((a1.x - b1.x) * dy2 - (a1.y - b1.y) * dx2) / det;
            if (t <= 0.0) ip = a1;
            else if (t >= 1.0) ip = a2;
            else ip = new float2(a1.x + t * dx1, a1.y + t * dy1);
            return true;
        }


        public struct XComparer : IComparer<float2>
        {
            public int Compare(float2 x, float2 y)
            {
                return x.x.CompareTo(y.x);
            }
        }

        /// <summary>Find the intersections (up to two) between a line and a quadratic bezier edge</summary>
        static void IntersectQuadraticBezierAndScanline(NativeList<SDFEdge> edges, int edgeID, int startID, int nextStartID, int ys, int minX, int maxX, NativeList<float2> intersectionPoints)
        {
            var edge = edges[edgeID];
            var x0 = edge.start_pos.x;
            var y0 = edge.start_pos.y;
            var x1 = edge.control1.x;
            var y1 = edge.control1.y;
            var x2 = edge.end_pos.x;
            var y2 = edge.end_pos.y;

            var a = y2 - 2 * y1 + y0;
            var b = 2 * y1 - 2 * y0;
            var c = y0 - ys;

            var rootCount = PaintUtils.QuadraticRoots(a, b, c, out float2 roots, out bool tangent);

            //ranges that need to be filled require two crossings of the scanline with the bezier curve
            //if the scanline is tangent to the bezier edge (=not crossing, only 1 root), then do not add intersection point
            //as there will be no pair/fill range (==>raserization artifacts)
            if (rootCount == 0 || tangent) 
                return;

            // calc the solution points
            for (var i = 0; i < rootCount; i++)
            {
                var t = roots[i];
                if (t >= 0 && t <= 1)
                {
                    var intersectPointX = math.lerp(math.lerp(x0, x1, t), math.lerp(x1, x2, t), t);

                    // See if point is on line segment
                    if (minX <= intersectPointX && intersectPointX <= maxX)
                    {
                        var intersectPoint = new float2(intersectPointX, ys);

                        if(IntersectionAtExposedVertex(t == 0, t == 1, edgeID, startID, nextStartID, edges, new float2(minX, ys), intersectPoint))
                            continue;

                        if (intersectionPoints.Contains(intersectPoint) && DoNotDoublicateIntersectionPoint(t == 0, t == 1, edgeID, startID, nextStartID, edges))
                            continue;
                        intersectionPoints.Add(intersectPoint);
                    }
                }
            }
        }
        
        static bool DoNotDoublicateIntersectionPoint(bool intersectAtStartPos, bool intersectAtEndPos, int edgeID, int startID, int nextStartID, NativeList<SDFEdge> edges)
        {
            // If intersecting point has already been found, it means the scanline is right at a vertex shared by two edges. 
            // For the given bezier contour, the two egdes could be previous.end & current.start and, and in case of the last edge also
            // LastEdge.end & FirstEdge.start (because outlines are closed polygons). Check if the two edges are identical (same ymin and same ymax).
            // If identical, add the intersection point again (why?)            

            var edge = edges[edgeID];
            var prevEdge = edgeID == startID ? edges[nextStartID - 1] : edges[edgeID - 1];
            var nextEdge = edgeID == nextStartID - 1 ? edges[startID] : edges[edgeID + 1];


            var edgeYmin = math.min(edge.start_pos.y, edge.end_pos.y);
            var edgeYmax = math.max(edge.start_pos.y, edge.end_pos.y);

            if (intersectAtStartPos)
            {
                var prevEdgeYmin = math.min(prevEdge.start_pos.y, prevEdge.end_pos.y);
                var prevEdgeYmax = math.max(prevEdge.start_pos.y, prevEdge.end_pos.y);

                if (prevEdgeYmin == edgeYmin && prevEdgeYmax == edgeYmax)
                    return false;
            }
            else if (intersectAtEndPos)
            {
                var nextEdgeYmin = math.min(nextEdge.start_pos.y, nextEdge.end_pos.y);
                var nextEdgeYmax = math.max(nextEdge.start_pos.y, nextEdge.end_pos.y);

                if (nextEdgeYmin == edgeYmin && nextEdgeYmax == edgeYmax)
                    return false;
            }
            return true;
        }
        static bool IntersectionAtExposedVertex(bool intersectAtStartPos, bool intersectAtEndPos, int edgeID, int startID, int nextStartID, NativeList<SDFEdge> edges, float2 scanLineStart, float2 intersectPoint)
        {
            //additional check needed for even-odd filling when intersectPoint is a vertex
            //https://www.geeksforgeeks.org/even-odd-method-winding-number-method-inside-outside-test-of-a-polygon/
            //needs additional work because it can currently not discriminate all scenarios

            var edge = edges[edgeID];
            if (intersectAtStartPos)
            {
                var prevEdge = edgeID == startID ? edges[nextStartID - 1] : edges[edgeID - 1];
                double res1 = Orient2DFast(prevEdge.start_pos, scanLineStart, intersectPoint);
                double res2 = Orient2DFast(edge.end_pos, scanLineStart, intersectPoint);
                if (res1 * res2 > 0) // scanline is just touching prevEdge-->Edge and not crossing
                    return true;
            }
            else if (intersectAtEndPos)
            {
                var nextEdge = edgeID == nextStartID - 1 ? edges[startID] : edges[edgeID + 1];
                double res1 = Orient2DFast(nextEdge.end_pos, scanLineStart, intersectPoint);
                double res2 = Orient2DFast(edge.start_pos, scanLineStart, intersectPoint);
                if (res1 * res2 > 0) // scanline is just touching Edge-->nextedge and not crossing
                   return true;
            }
            return false;
        }
    }
}
