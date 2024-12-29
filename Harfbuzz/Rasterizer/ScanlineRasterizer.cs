using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    public static class ScanlineRasterizer
    {
        public static void Rasterize(ref DrawData drawData, NativeArray<ColorARGB> textureData, ColorARGB color, int width, int height, bool inverse=false)
        {            
            Debug.Log($"Rasterize with color {color}");

            var intersectionPoints = new NativeList<float2> (256, Allocator.Temp);

            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var step = 1;
            var xmin= drawData.glyphRect.min.x - 1;
            var xmax = drawData.glyphRect.max.x + 1;
            var ymin = drawData.glyphRect.min.y;
            var ymax = drawData.glyphRect.max.y;

            var scanLineStart = new float2(xmin, ymin);
            var scanLineEnd = new float2(xmax, ymin);

            for (float y = ymin; y < ymax + 1; y += step)
            {
                scanLineStart.y = y; scanLineEnd.y = y;
                intersectionPoints.Clear();
                if(inverse)
                    intersectionPoints.Add(new float2(xmin,y));
                float2 previntersectPoint = float.NegativeInfinity;

                for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
                {
                    int startID = contourIDs[contourID];
                    int nextStartID = contourIDs[contourID + 1];
                    for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                    {
                        var edge = edges[edgeID];
                        bool intersect = EdgesIntersect(scanLineStart, scanLineEnd, edge.start_pos, edge.end_pos, true);
                        if (intersect)
                        {                            
                            GetIntersectPt(scanLineStart, scanLineEnd, edge.start_pos, edge.end_pos, out float2 intersectPoint);
                            //Special Case Handeling
                            // Case when intersection point is a vertex
                            // if the prevs point is the same as current point, means the point is a vertex,
                            // Check the prev line and current line if both ymin is the same
                            // if same, add again
                            if (math.all(previntersectPoint == intersectPoint))
                            {
                                var prevEdge = edges[edgeID - 1];
                                var prevEdgeYmin = math.min(prevEdge.start_pos.y, prevEdge.end_pos.y);
                                var edgeYmin = math.min(edge.start_pos.y, edge.end_pos.y);

                                var prevEdgeYmax = math.max(prevEdge.start_pos.y, prevEdge.end_pos.y);
                                var edgeYmax = math.max(edge.start_pos.y, edge.end_pos.y);

                                if (prevEdgeYmin != edgeYmin && prevEdgeYmax != edgeYmax)
                                {
                                    continue;
                                }
                            }

                            intersectionPoints.Add(intersectPoint);

                            previntersectPoint = intersectPoint;
                        }
                    }
                }
                if (inverse)
                    intersectionPoints.Add(new float2(xmax, y));
                intersectionPoints.Sort(default(XComparer));

                for (int i = 0; i < intersectionPoints.Length - 1; i += 2)
                {
                    var startX = (int)intersectionPoints[i].x;
                    var endX = (int)intersectionPoints[i + 1].x;

                    for (int column = startX; column < endX; column += step)
                    {
                        var targetIndex = width * (int)y + column;
                        //textureData[targetIndex] = textureColor;

                        //var ColorSrc = textureColor;
                        //var colorDest = textureData[targetIndex];
                        //float src = 0.5f, dest  = 0.5f;
                        //Color32 ColorRes=default;
                        //ColorRes.r = (byte)(ColorSrc.r * src + colorDest.r * dest);
                        //ColorRes.g = (byte)(ColorSrc.g * src + colorDest.g * dest);
                        //ColorRes.b = (byte)(ColorSrc.b * src + colorDest.b * dest);
                        //ColorRes.a = (byte)(ColorSrc.a * src + colorDest.a * dest);
                        //textureData[targetIndex] = ColorRes;

                        int aa = 255;
                        int fa = color.a * aa / 255;

                        int fb = color.b * fa / 255;
                        int fg = color.g * fa / 255;
                        int fr = color.r * fa / 255;

                        int ba2 = 255 - fa;

                        var colorDest = textureData[targetIndex];
                        int bb = colorDest.b;
                        int bg = colorDest.g;
                        int br = colorDest.r;
                        int ba = colorDest.a;

                        colorDest.b = (byte)(bb * ba2 / 255 + fb);
                        colorDest.g = (byte)(bg * ba2 / 255 + fg);
                        colorDest.r = (byte)(br * ba2 / 255 + fr);
                        colorDest.a = (byte)(ba * ba2 / 255 + fa);
                        textureData[targetIndex] = colorDest;
                    }
                }
            }
        }
        public static void RasterizeLinearGradient(ref DrawData drawData, NativeArray<ColorARGB> textureData, NativeArray<ColorStop> colorStops, float2 p0, float2 p1, float2 p2,int width, int height, bool inverse = false)
        {
            
            var colorA = colorStops[0].color;
            var colorB = colorStops[1].color;
            Debug.Log($"Rasterize with colorline from {colorA} to {colorB}");

            var intersectionPoints = new NativeList<float2>(256, Allocator.Temp);

            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var step = 1;
            var xmin = drawData.glyphRect.min.x - 1;
            var xmax = drawData.glyphRect.max.x + 1;
            var ymin = drawData.glyphRect.min.y;
            var ymax = drawData.glyphRect.max.y;

            var scanLineStart = new float2(xmin, ymin);
            var scanLineEnd = new float2(xmax, ymin);

            for (float y = ymin; y < ymax + 1; y += step)
            {
                scanLineStart.y = y; scanLineEnd.y = y;
                intersectionPoints.Clear();
                if (inverse)
                    intersectionPoints.Add(new float2(xmin, y));
                float2 previntersectPoint = float.NegativeInfinity;

                for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
                {
                    int startID = contourIDs[contourID];
                    int nextStartID = contourIDs[contourID + 1];
                    for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                    {
                        var edge = edges[edgeID];
                        bool intersect = EdgesIntersect(scanLineStart, scanLineEnd, edge.start_pos, edge.end_pos, true);
                        if (intersect)
                        {
                            GetIntersectPt(scanLineStart, scanLineEnd, edge.start_pos, edge.end_pos, out float2 intersectPoint);
                            //Special Case Handeling
                            // Case when intersection point is a vertex
                            // if the prevs point is the same as current point, means the point is a vertex,
                            // Check the prev line and current line if both ymin is the same
                            // if same, add again
                            if (math.all(previntersectPoint == intersectPoint))
                            {
                                var prevEdge = edges[edgeID - 1];
                                var prevEdgeYmin = math.min(prevEdge.start_pos.y, prevEdge.end_pos.y);
                                var edgeYmin = math.min(edge.start_pos.y, edge.end_pos.y);

                                var prevEdgeYmax = math.max(prevEdge.start_pos.y, prevEdge.end_pos.y);
                                var edgeYmax = math.max(edge.start_pos.y, edge.end_pos.y);

                                if (prevEdgeYmin != edgeYmin && prevEdgeYmax != edgeYmax)
                                {
                                    continue;
                                }
                            }

                            intersectionPoints.Add(intersectPoint);

                            previntersectPoint = intersectPoint;
                        }
                    }
                }
                if (inverse)
                    intersectionPoints.Add(new float2(xmax, y));
                intersectionPoints.Sort(default(XComparer));

                for (int i = 0; i < intersectionPoints.Length - 1; i += 2)
                {
                    var startX = (int)intersectionPoints[i].x;
                    var endX = (int)intersectionPoints[i + 1].x;

                    for (int column = startX; column < endX; column += step)
                    {
                        var t = (float)(column - p0.x) / (float)(p1.x - p0.x);
                        var color = ColorARGB.Lerp(colorA, colorB,t);
                        
                        var targetIndex = width * (int)y + column;
                        //textureData[targetIndex] = textureColor;

                        //var ColorSrc = textureColor;
                        //var colorDest = textureData[targetIndex];
                        //float src = 0.5f, dest  = 0.5f;
                        //Color32 ColorRes=default;
                        //ColorRes.r = (byte)(ColorSrc.r * src + colorDest.r * dest);
                        //ColorRes.g = (byte)(ColorSrc.g * src + colorDest.g * dest);
                        //ColorRes.b = (byte)(ColorSrc.b * src + colorDest.b * dest);
                        //ColorRes.a = (byte)(ColorSrc.a * src + colorDest.a * dest);
                        //textureData[targetIndex] = ColorRes;

                        int aa = 255;
                        int fa = color.a * aa / 255;

                        int fb = color.b * fa / 255;
                        int fg = color.g * fa / 255;
                        int fr = color.r * fa / 255;

                        int ba2 = 255 - fa;

                        var colorDest = textureData[targetIndex];
                        int bb = colorDest.b;
                        int bg = colorDest.g;
                        int br = colorDest.r;
                        int ba = colorDest.a;

                        colorDest.b = (byte)(bb * ba2 / 255 + fb);
                        colorDest.g = (byte)(bg * ba2 / 255 + fg);
                        colorDest.r = (byte)(br * ba2 / 255 + fr);
                        colorDest.a = (byte)(ba * ba2 / 255 + fa);
                        textureData[targetIndex] = colorDest;
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
                //reports intersection only when edge points are on opposite site of th eother edge
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
        enum SkBlendMode
        {
            kClear,         //!< r = 0
            kSrc,           //!< r = s
            kDst,           //!< r = d
            kSrcOver,       //!< r = s + (1-sa)*d
            kDstOver,       //!< r = d + (1-da)*s
            kSrcIn,         //!< r = s * da
            kDstIn,         //!< r = d * sa
            kSrcOut,        //!< r = s * (1-da)
            kDstOut,        //!< r = d * (1-sa)
            kSrcATop,       //!< r = s*da + d*(1-sa)
            kDstATop,       //!< r = d*sa + s*(1-da)
            kXor,           //!< r = s*(1-da) + d*(1-sa)
            kPlus,          //!< r = min(s + d, 1)
            kModulate,      //!< r = s*d
            kScreen,        //!< r = s + d - s*d

            kOverlay,       //!< multiply or screen, depending on destination
            kDarken,        //!< rc = s + d - max(s*da, d*sa), ra = kSrcOver
            kLighten,       //!< rc = s + d - min(s*da, d*sa), ra = kSrcOver
            kColorDodge,    //!< brighten destination to reflect source
            kColorBurn,     //!< darken destination to reflect source
            kHardLight,     //!< multiply or screen, depending on source
            kSoftLight,     //!< lighten or darken, depending on source
            kDifference,    //!< rc = s + d - 2*(min(s*da, d*sa)), ra = kSrcOver
            kExclusion,     //!< rc = s + d - two(s*d), ra = kSrcOver
            kMultiply,      //!< r = s*(1-da) + d*(1-sa) + s*d

            kHue,           //!< hue of source with saturation and luminosity of destination
            kSaturation,    //!< saturation of source with hue and luminosity of destination
            kColor,         //!< hue and saturation of source with luminosity of destination
            kLuminosity,    //!< luminosity of source with hue and saturation of destination

            kLastCoeffMode     = kScreen,     //!< last porter duff blend mode
            kLastSeparableMode = kMultiply,   //!< last blend mode operating separately on components
            kLastMode          = kLuminosity, //!< last valid value
        };

    }
}
