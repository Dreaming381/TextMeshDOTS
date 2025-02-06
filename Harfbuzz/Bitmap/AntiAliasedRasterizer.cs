using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{
    /*
        description of rasterizer: https://medium.com/@raphlinus/inside-the-fastest-font-renderer-in-the-world-75ae5270c445
        https://github.com/raphlinus/font-rs/tree/master
        Copyright 2015 Google Inc. All rights reserved.

        Licensed under the Apache License, Version 2.0 (the "License");
        you may not use this file except in compliance with the License.
        You may obtain a copy of the License at
            http://www.apache.org/licenses/LICENSE-2.0
        Unless required by applicable law or agreed to in writing, software
        distributed under the License is distributed on an "AS IS" BASIS,
        WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
        See the License for the specific language governing permissions and
        limitations under the License.

        An antialiased rasterizer for quadratic Beziers
    */
    [BurstCompile]
    public static class AntiAliasedRasterizer
    {
        public static void Rasterize<T>(ref DrawData drawData, NativeArray<ColorARGB> textureData, T pattern, BBox clipRect, bool invert = false) where T : IPattern
        {
            PaintUtils.rasterizeMarker.Begin();
            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var width = clipRect.intWidth;
            var height = clipRect.intHeight;
            var areas = new NativeArray<float>(width * height, Allocator.Temp);
            var offset = clipRect.min;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    var edge = edges[edgeID];
                    var p0 = edge.start_pos - offset;
                    var p1 = edge.end_pos - offset;
                    bool inverse = p1.y < p0.y;
                    var dir = math.select(1.0f, -1.0f, inverse);
                    if (inverse)
                        (p0, p1) = (p1, p0);
                    var dxdy = (p1.x - p0.x) / (p1.y - p0.y);
                    var x = p0.x;
                    var y0 = (int)p0.y;
                    x = math.select(x, x - p0.y * dxdy, x < 0.0f);
                    for (int y = y0, yy = math.min(height, (int)math.ceil(p1.y)); y < yy; ++y)
                    {
                        var linestart = y * width;
                        var sy0 = math.max(y, p0.y);
                        var sy1 = math.min(y + 1, p1.y);
                        var dy = sy1 - sy0;
                        var xnext = x + dxdy * dy;
                        var d = dy * dir;
                        var x0 = math.select(xnext, x, x < xnext);
                        var x1 = math.select(x, xnext, x < xnext);
                        var x0floor = math.floor(x0);
                        var x0i = (int)x0floor;
                        var x1ceil = math.ceil(x1);
                        var x1i = (int)x1ceil;
                        if (x1i <= x0i + 1)
                        {
                            var linestart_x0i = linestart + x0i;
                            if (linestart_x0i < 0)  // index is out of bounds 
                                continue;

                            // simple case, edge only crosses one pixel in current line (includes vertical edge case)
                            var xmf = 0.5f * (x0 + x1) - x0floor;
                            areas[linestart_x0i] += d - d * xmf; // area of trapezoid in pixel
                            areas[linestart_x0i + 1] += d * xmf; // everything right of this pixel is filled
                        }
                        else
                        {
                            var linestart_x0i = linestart + x0i;
                            if (linestart_x0i < 0)  // index is out of bounds 
                                continue;

                            var s = math.rcp(x1 - x0);
                            var x0f = x0 - x0floor;
                            var a0 = 0.5f * (1.0f - x0f) * (1.0f - x0f) * s;
                            var x1f = x1 - x1ceil + 1.0f;
                            var am = 0.5f * x1f * x1f * s;

                            areas[linestart_x0i] += d * a0;         //area of triangle in first pixel crossed by edge
                            if (x1i == x0i + 2)
                                areas[linestart_x0i + 1] += d * (1.0f - a0 - am); //area of trapezoid between first and last pixel crossed by edge
                            else
                            {
                                var a1 = (1.5f - x0f) * s;
                                areas[linestart_x0i + 1] += d * (a1 - a0); //area of trapezoid between first and last pixel crossed by edge
                                for (int xi = x0i + 2, xii = x1i - 1; xi < xii; xi++)
                                    areas[linestart + xi] += d * s;        //area of trapezoid between first and last pixel crossed by edge
                                var a2 = a1 + (x1i - x0i - 3) * s;
                                areas[linestart + (x1i - 1)] += d * (1.0f - a2 - am);   ///area of trapezoid in last pixel crossed by edge
                            }
                            areas[linestart + x1i] += d * am; // everything right of this pixel is filled
                        }
                        x = xnext;
                    }
                }
            }

            //this loop is ~15 % of rendering time(so not much SIMD speedup potential)            
            for (int y = 0; y < height; y++)
            {
                float sum = 0;//important to reset sum at every line start to not accumulate errors over the entire picture
                var linestart = y * width;
                for (int x = 0; x < width; x++)
                {
                    var index = linestart + x;
                    sum += areas[index];
                    var alpha = math.abs(sum);
                    alpha = math.select(1.0f, alpha, alpha < 1.0f);
                    var alphaByte = (byte)(255 * alpha);
                    if (alphaByte > 1)
                    {
                        var color = pattern.GetColor(new float2(x, y) + offset);
                        color.a = (byte)(color.a * alphaByte / 255);
                        textureData[index] = color;
                    }
                }
            }
            PaintUtils.rasterizeMarker.End();
        }

        public static void RasterizeAndBlend<T>(ref DrawData drawData, NativeArray<ColorARGB> textureData, T pattern, PaintCompositeMode mode, BBox clipRect, bool invert = false) where T : IPattern
        {
            PaintUtils.rasterizeMarker.Begin();
            var sdfEdges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var width = clipRect.intWidth;
            var height = clipRect.intHeight;
            var areas = new NativeArray<float>(width * height, Allocator.Temp);
            var offset = clipRect.min;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    var sdfEdge = sdfEdges[edgeID];
                    var p0 = sdfEdge.start_pos - offset;
                    var p1 = sdfEdge.end_pos - offset;
                    bool inverse = p0.y < p1.y;
                    var dir = math.select(-1.0f, 1.0f, inverse);
                    if (!inverse)
                        (p0, p1) = (p1, p0);
                    var dx = (p1.x - p0.x);
                    var dy = (p1.y - p0.y);
                    var dxdy = (p1.x - p0.x) / (p1.y - p0.y);
                    var x = p0.x;
                    var y0 = (int)p0.y;
                    x = math.select(x, x - p0.y * dxdy, p0.y < 0.0f);
                    for (int y = y0, yy = math.min(height, (int)math.ceil(p1.y)); y < yy; ++y)
                    {
                        var linestart = y * width;
                        dy = math.min(y + 1, p1.y) - math.max(y, p0.y);
                        var xnext = dy == 0 ? x + dx : x + dxdy * dy;
                        //var xnext = x + dxdy * dy;
                        var d = dy * dir;
                        var x0 = math.select(xnext, x, x < xnext);
                        var x1 = math.select(x, xnext, x < xnext);
                        var x0floor = math.floor(x0);
                        var x0i = (int)x0;
                        var x1ceil = math.ceil(x1);
                        var x1i = (int)x1;
                        if (x1i <= x0i + 1)
                        {
                            var xmf = 0.5f * (x + xnext) - x0floor;
                            var linestart_x0i = linestart + x0i;
                            if (linestart_x0i < 0)  // index is out of bounds 
                                continue;
                            areas[linestart_x0i] += d - d * xmf;
                            areas[linestart_x0i + 1] += d * xmf;
                        }
                        else
                        {
                            var s = math.rcp(x1 - x0);
                            var x0f = x0 - x0floor;
                            var a0 = 0.5f * s * (1.0f - x0f) * (1.0f - x0f);
                            var x1f = x1 - x1ceil + 1.0f;
                            var am = 0.5f * s * x1f * x1f;
                            var linestart_x0i = linestart + x0i;
                            if (linestart_x0i < 0)  // index is out of bounds 
                                continue;
                            areas[linestart_x0i] += d * a0;
                            if (x1i == x0i + 2)
                                areas[linestart_x0i + 1] += d * (1.0f - a0 - am);
                            else
                            {
                                var a1 = s * (1.5f - x0f);
                                areas[linestart_x0i + 1] += d * (a1 - a0);
                                for (int xi = x0i + 2, xii = x1i - 1; xi < xii; xi++)
                                    areas[linestart + xi] += d * s;
                                var a2 = a1 + (x1i - x0i - 3) * s;
                                areas[linestart + (x1i - 1)] += d * (1.0f - a2 - am);
                            }
                            areas[linestart + x1i] += d * am;
                        }
                        x = xnext;
                    }
                }
            }

            //this loop is ~15 % of rendering time(so not much SIMD speedup potential)            
            for (int y = 0; y < height; y++)
            {
                float sum = 0;//important to reset sum at every line start to not accumulate errors over the entire picture
                var linestart = y * width;
                for (int x = 0; x < width; x++)
                {
                    var index = linestart + x;
                    sum += areas[index];
                    var alpha = math.abs(sum);
                    alpha = math.select(1.0f, alpha, alpha < 1.0f);
                    var alphaByte = (byte)(255 * alpha);
                    if (alphaByte > 1)
                    {
                        var color = pattern.GetColor(new float2(x, y) + offset);
                        color.a = (byte)(color.a * alphaByte / 255);
                        textureData[index] = Blending.Blend(color, textureData[index], mode);
                    }
                }
            }
            PaintUtils.rasterizeMarker.End();
        }
    }
}