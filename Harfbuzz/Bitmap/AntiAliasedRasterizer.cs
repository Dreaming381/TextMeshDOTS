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
            var sdfEdges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var width = (int)clipRect.width;
            var height = (int)clipRect.height;
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
                    var dxdy = (p1.x - p0.x) / (p1.y - p0.y);
                    var x = p0.x;
                    var y0 = (int)p0.y;
                    x = math.select(x, x - p0.y * dxdy, x < 0.0f);
                    for (int y = y0, yy = math.min(height, (int)math.ceil(p1.y)); y < yy; ++y)
                    {
                        var linestart = y * width;
                        var dy = math.min(y + 1,p1.y) - math.max(y,p0.y);
                        var xnext = x + dxdy * dy;
                        var d = dy * dir;
                        var x0 = math.select(xnext, x, x < xnext);
                        var x1 = math.select(x, xnext, x < xnext);
                        var x0floor = math.floor(x0);
                        var x0i = (int)x0;
                        var x1ceil = math.ceil(x1);
                        var x1i = (int)x1;
                        if( x1i <= x0i + 1)
                        {
                            var xmf = 0.5f * (x + xnext) - x0floor;
                            var linestart_x0i = linestart + x0i;
                            if(linestart_x0i < 0)  // index is out of bounds 
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
                            if(x1i == x0i + 2)
                                areas[linestart_x0i + 1] += d * (1.0f - a0 - am);
                            else
                            {
                                var a1 = s * (1.5f - x0f);
                                areas[linestart_x0i + 1] += d * (a1 - a0);
                                for(int xi = x0i + 2, xii = x1i - 1; xi<xii; xi++)
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
            float sum = 0;
            for (int i = 0, ii = areas.Length; i < ii; i++)
            {
                sum += areas[i];
                var alpha = math.abs(sum);
                alpha = math.select(1.0f, alpha, alpha < 1.0f);
                var alphaByte = (byte)(255 * alpha);
                if (alphaByte > 1)
                {
                    var row = i / width;
                    var column = i % width;
                    var color = pattern.GetColor(new float2(column, row)+offset);
                    color.a = (byte)(color.a * alphaByte / 255);
                    textureData[i] = color;
                }
            }

            PaintUtils.rasterizeMarker.End();
        }

        public static void RasterizeAndBlend<T>(ref DrawData drawData, NativeArray<ColorARGB> textureData, T pattern, PaintCompositeMode mode, BBox clipRect, bool invert = false) where T : IPattern
        {
            PaintUtils.rasterizeMarker.Begin();
            var sdfEdges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            var width = (int)clipRect.width;
            var height = (int)clipRect.height;
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
                    var dxdy = (p1.x - p0.x) / (p1.y - p0.y);
                    var x = p0.x;
                    var y0 = (int)p0.y;
                    x = math.select(x, x - p0.y * dxdy, x < 0.0f);
                    for (int y = y0, yy = math.min(height, (int)math.ceil(p1.y)); y < yy; ++y)
                    {
                        var linestart = y * width;
                        var dy = math.min(y + 1, p1.y) - math.max(y, p0.y);
                        var xnext = x + dxdy * dy;
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
            float sum = 0;
            for (int i = 0, ii = areas.Length; i < ii; i++)
            {
                sum += areas[i];
                var alpha = math.abs(sum);
                alpha = math.select(1.0f, alpha, alpha < 1.0f);
                var alphaByte = (byte)(255 * alpha);
                if (alphaByte > 1)
                {
                    var row = i / width;
                    var column = i % width;
                    var color = pattern.GetColor(new float2(column, row) + offset);
                    color.a = (byte)(color.a * alphaByte / 255);                    
                    textureData[i] = Blending.Blend(color, textureData[i], mode);
                }
            }

            PaintUtils.rasterizeMarker.End();
        }
        
    }   
}