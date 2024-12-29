using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TextCore;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    public static class SDF
    {
        //generates SDF directly from Bezier Data that are provided by Harfbuzz
        //approach is inspired by FreeType 
        public static SignedDistance max_sdf => new SignedDistance { distance = int.MaxValue, sign = 0, cross = 0 };

        public const float CORNER_CHECK_EPSILON = 32 / (1 << 16); //The epsilon distance  used for corner

        public static bool SDFGenerateSubDivision(SDFOrientation orientation, ref DrawData drawData, NativeArray<byte> buffer, GlyphRect glyphRect, int atlasWidth, int atlasHeight, bool splitBezierToLines= true, int spread = SDFCommon.DEFAULT_SPREAD)
        {
            var success = true;
            if (drawData.contourIDs.Length < 2 || drawData.edges.Length == 0)
                return false;

            //SDFCommon.WriteGlyphOutlineToFile("BeforeSplit_FOR.txt", drawData);
            if (splitBezierToLines)
            {
                success = SplitSDFShape(ref drawData, out DrawData newBezierData);
                success = SDFGenerateBoundingBox(ref newBezierData, orientation, spread, buffer, glyphRect, atlasWidth, atlasHeight);
            }
            else
                success = SDFGenerateBoundingBox(ref drawData, orientation, spread, buffer, glyphRect, atlasWidth, atlasHeight);
            return success;
        }
        
        static bool SplitSDFShape(ref DrawData drawData, out DrawData newBezierData)
        {
            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            newBezierData = new DrawData(edges.Length * 16, contourIDs.Length, Allocator.Temp);
            var newEdges = newBezierData.edges;
            var newContourIDs = newBezierData.contourIDs;

            bool success = true;
            SDFEdge edge;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                newContourIDs.Add(newEdges.Length);
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                float dx;
                int num_splits;
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    edge = edges[edgeID];
                    switch (edge.edge_type)
                    {
                        case SDFEdgeType.LINE:
                            newEdges.Add(edge);
                            break;
                        case SDFEdgeType.QUADRATIC:
                            dx = GetDeviationQuadratic(edges, edgeID);                            
                            if (dx > SDFCommon.MAX_DEVIATION_SPLITTING)
                            {
                                num_splits = 1;
                                while (dx > SDFCommon.MAX_DEVIATION_SPLITTING)
                                {                                    
                                    dx /= 4;
                                    num_splits *= 2;
                                }
                                newEdges.AddRange(SplitQuadraticEdge(edge, num_splits));
                            }
                            else
                            {
                                edge.edge_type = SDFEdgeType.LINE;
                                newEdges.Add(edge);
                            }
                            break;
                        case SDFEdgeType.CUBIC:
                            dx = GetDeviationCubic(edges, edgeID);
                            if (dx > SDFCommon.MAX_DEVIATION_SPLITTING)
                            {
                                num_splits = 1;
                                while (dx > SDFCommon.MAX_DEVIATION_SPLITTING)
                                {
                                    dx /= 4;
                                    num_splits *= 2;
                                }
                                newEdges.AddRange(SplitCubicEdge(edge, num_splits));
                            }
                            else
                            {
                                edge.edge_type = SDFEdgeType.LINE;
                                newEdges.Add(edge);
                            }
                            break;

                        default:
                            break;
                    }
                }
            }
            newContourIDs.Add(newEdges.Length);//close the last contour
            return success;
        }
        static float GetDeviationQuadratic(NativeList<SDFEdge> edgeList, int edgeID)
        {
            var quadratic = edgeList[edgeID];
            var A = quadratic.start_pos;
            var B = quadratic.control1;
            var C = quadratic.end_pos;

            var d1 = math.abs(C + A - 2 * B);
            return math.max(d1.x, d1.y);
        }

        static float GetDeviationCubic(NativeList<SDFEdge> edgeList, int edgeID)
        {
            var quadratic = edgeList[edgeID];
            var A = quadratic.start_pos;
            var B = quadratic.control1;
            var C = quadratic.control2;
            var D = quadratic.end_pos;

            var d1 = math.abs(2 * A - 3 * B + D);
            var d2 = math.abs(A - 3 * C + 2 * D);

            return math.max(math.max(d1.x, d1.y), math.max(d2.x, d2.y));
        }

        
        /// <summary> This function splits a quadratic bezier into two quadratic bezier exactly half way at t = 0.5. </summary>
        static NativeArray<SDFEdge> SplitQuadraticEdge(SDFEdge edge, int num_splits)
        {
            int numRows = 1 << (num_splits);
            var targetArray = new NativeArray<SDFEdge>(numRows, Allocator.Temp);
            targetArray[0] = edge;

            for (int split = num_splits - 1; split >= 0; split--)
            {
                int pairDistance = 1 << split;

                for (int row = 0; row < numRows - pairDistance; row += 2 * pairDistance)
                {
                    var sourceEdge = targetArray[row];
                    var A = sourceEdge.start_pos;
                    var B = sourceEdge.control1;
                    var C = sourceEdge.end_pos;

                    var D = (A + B) * 0.5f;
                    var E = (B + C) * 0.5f;
                    var F = (D + E) * 0.5f;

                    sourceEdge.start_pos = A;
                    sourceEdge.control1 = D;
                    sourceEdge.end_pos = F;
                    sourceEdge.edge_type = SDFEdgeType.LINE;
                    targetArray[row]=sourceEdge;

                    targetArray[row + pairDistance] = new SDFEdge
                    {
                        start_pos = F,
                        control1 = E,
                        end_pos = C,
                        edge_type = SDFEdgeType.LINE,
                    };
                }
            }
            return targetArray;
        }

        /// <summary> This function splits a cubic bezier into two cubic bezier exactly half way at t = 0.5. </summary>
        static NativeArray<SDFEdge> SplitCubicEdge(SDFEdge edge, int num_splits)
        {
            int numRows = 1 << (num_splits);
            var targetArray = new NativeArray<SDFEdge>(numRows, Allocator.Temp);
            targetArray[0] = edge;

            for (int split = num_splits - 1; split >= 0; split--)
            {
                int pairDistance = 1 << split;

                for (int row = 0; row < numRows - pairDistance; row += 2 * pairDistance)
                {
                    var sourceEdge = targetArray[row];
                    var A = sourceEdge.start_pos;
                    var B = sourceEdge.control1;
                    var C = sourceEdge.control2;
                    var D = sourceEdge.end_pos;

                    var E = (A + B) * 0.5f;
                    var F = (B + C) * 0.5f;
                    var G = (C + D) * 0.5f;
                    var H = (E + F) * 0.5f;
                    var J = (F + G) * 0.5f;
                    var K = (H + J) * 0.5f;

                    sourceEdge.start_pos = A;
                    sourceEdge.control1 = E;
                    sourceEdge.control2 = H;
                    sourceEdge.end_pos = K;
                    sourceEdge.edge_type = SDFEdgeType.LINE;
                    targetArray[row] = sourceEdge;

                    targetArray[row + pairDistance] = new SDFEdge
                    {
                        start_pos = K,
                        control1 = J,
                        control2 = G,
                        end_pos = D,
                        edge_type = SDFEdgeType.LINE,
                    };
                }
            }
            return targetArray;
        }
        
        static bool SDFGenerateBoundingBox(ref DrawData drawData, SDFOrientation orientation, int spread, NativeArray<byte> buffer, GlyphRect glyphRect, int atlasWidth, int atlasHeight)
        {
            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;

            bool flip_y = true;
            bool flip_sign = false;
            int overloadSign = 0;
            float sp_sq;
            SDFEdge edge;
            var dists = new NativeArray<SignedDistance>(glyphRect.width * glyphRect.height, Allocator.Temp);

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;

            if (SDFCommon.USE_SQUARED_DISTANCES)
                sp_sq = spread * spread;
            else
                sp_sq = spread;

            int maxIndex = int.MinValue;
            int minIndex = int.MaxValue;
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
                    BBox cbox;

                    cbox = GetControlBox(edge);
                    //cbox = GetBBox(edge); //BBox might be smaller--review if atlas can hold more glyphs using this 
                    cbox.Expand(spread);

                    /* now loop over the pixels in the control box. */
                    for (int y = (int)cbox.min.y, yEnd = (int)cbox.max.y; y < yEnd; y++)
                    {
                        for (int x = (int)cbox.min.x, xEnd = (int)cbox.max.x; x < xEnd; x++)
                        {
                            float2 grid_point;
                            SignedDistance dist = max_sdf;
                            int index = 0;
                            float diff = 0;

                            if (x < 0 || x >= rectWidth)
                                continue;
                            if (y < 0 || y >= rectHeight)
                                continue;

                            grid_point.x = x;
                            grid_point.y = y;

                            // use the center of any pixel to be rendered within cbox
                            grid_point.x += 1f / 2f;
                            grid_point.y += 1f / 2f;
                            SDFEdgeGetMinDistance(edge, grid_point, ref dist);

                            if (orientation == SDFOrientation.FILL_LEFT)
                                dist.sign = -dist.sign;

                            // ignore if the distance is greater than spread;
                            // otherwise it creates artifacts due to the wrong sign
                            if (dist.distance > sp_sq)
                                continue;

                            if (SDFCommon.USE_SQUARED_DISTANCES)
                                dist.distance = math.sqrt(dist.distance);

                            if (flip_y)
                                index = y * rectWidth + x;
                            else
                                index = (rectHeight - y - 1) * rectWidth + x;

                            if (index < minIndex)
                                minIndex = index;
                            if (index > maxIndex)
                                maxIndex = index;
                            
                            if (EqualsLargeValues(dists[index].sign, 0)) // check if the pixel is already set
                                dists[index] = dist;
                            else
                            {
                                diff = math.abs(dists[index].distance - dist.distance);

                                if (diff <= CORNER_CHECK_EPSILON)
                                    dists[index] = ResolveCorner(dists[index], dist);
                                else if (dists[index].distance > dist.distance)
                                    dists[index] = dist;
                            }
                        }
                    }
                }
            }

            // final pass
            int outsideSign = -1;
            for (int row = 0; row < rectHeight; row++)
            {
                /* We assume the starting pixel of each row is outside. */
                int current_sign = outsideSign;

                if (overloadSign != 0)
                    current_sign = overloadSign < 0 ? -1 : 1;

                for (int column = 0; column < rectWidth; column++)
                {
                    var sourceIndex = rectWidth * row + column;
                    var targetIndex = (atlasWidth * (row + rectY)) + (column + rectX);

                    // if the pixel is not set, its shortest distance is more than `spread`
                    var dist = dists[sourceIndex];                    
                    if (EqualsLargeValues(dist.sign, 0))
                    {
                        dist.sign = outsideSign;
                        dist.distance = -spread;
                    }
                    current_sign = dist.sign;

                    // clamp the distance
                    if (dist.distance > spread)
                        dist.distance = spread;

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
        static BBox GetControlBox(SDFEdge edge)
        {
            BBox cbox = BBox.Empty;
            bool is_set = false;


            switch (edge.edge_type)
            {
                case SDFEdgeType.CUBIC:
                    cbox.min = edge.control2;
                    cbox.max = edge.control2;

                    is_set = true;
                    goto case SDFEdgeType.QUADRATIC;

                case SDFEdgeType.QUADRATIC:
                    if (is_set)
                    {
                        cbox.min.x = edge.control1.x < cbox.min.x ? edge.control1.x : cbox.min.x;
                        cbox.min.y = edge.control1.y < cbox.min.y ? edge.control1.y : cbox.min.y;

                        cbox.max.x = edge.control1.x > cbox.max.x ? edge.control1.x : cbox.max.x;
                        cbox.max.y = edge.control1.y > cbox.max.y ? edge.control1.y : cbox.max.y;
                    }
                    else
                    {
                        cbox.min = edge.control1;
                        cbox.max = edge.control1;

                        is_set = true;
                    }
                    goto case SDFEdgeType.LINE;

                case SDFEdgeType.LINE:
                    if (is_set)
                    {
                        cbox.min.x = edge.start_pos.x < cbox.min.x ? edge.start_pos.x : cbox.min.x;
                        cbox.max.x = edge.start_pos.x > cbox.max.x ? edge.start_pos.x : cbox.max.x;

                        cbox.min.y = edge.start_pos.y < cbox.min.y ? edge.start_pos.y : cbox.min.y;
                        cbox.max.y = edge.start_pos.y > cbox.max.y ? edge.start_pos.y : cbox.max.y;
                    }
                    else
                    {
                        cbox.min = edge.start_pos;
                        cbox.max = edge.start_pos;
                    }

                    cbox.min.x = edge.end_pos.x < cbox.min.x ? edge.end_pos.x : cbox.min.x;
                    cbox.max.x = edge.end_pos.x > cbox.max.x ? edge.end_pos.x : cbox.max.x;

                    cbox.min.y = edge.end_pos.y < cbox.min.y ? edge.end_pos.y : cbox.min.y;
                    cbox.max.y = edge.end_pos.y > cbox.max.y ? edge.end_pos.y : cbox.max.y;

                    break;

                default:
                    break;
            }

            return cbox;
        }
        static BBox GetBBox(SDFEdge edge)
        {
            switch (edge.edge_type)
            {
                case SDFEdgeType.CUBIC:
                    return BBox.GetCubicBezierBBox(edge.start_pos, edge.control1, edge.control2, edge.end_pos);
                case SDFEdgeType.QUADRATIC:
                    return BBox.GetQuadraticBezierBBox(edge.start_pos, edge.control1, edge.end_pos);
                case SDFEdgeType.LINE:
                    return BBox.GetLineBBox(edge.start_pos, edge.end_pos);
                default:
                    break;
            }
            return BBox.Empty;
        }
        public static bool SDFEdgeGetMinDistance(SDFEdge edge, float2 point, ref SignedDistance signedDistance)
        {
            bool success = false;
            switch (edge.edge_type)
            {
                case SDFEdgeType.LINE:
                    success = GetMinDistanceLine(edge, point, ref signedDistance);
                    break;
                case SDFEdgeType.QUADRATIC:
                    success = GetMinDistanceQuadraticNewton(edge, point, ref signedDistance);
                    break;
                case SDFEdgeType.CUBIC:
                    success = GetMinDistanceCubicNewton(edge, point, ref signedDistance);
                    break;
                default:
                    break;
            }
            return success;
        }
        static bool GetMinDistanceLine(SDFEdge line, float2 point, ref SignedDistance signedDistance)
        {
            var a = line.start_pos;
            var b = line.end_pos;

            var line_segment = b - a;                           //Vector from A to B
            var p_sub_a = point - a;                            //Vector from A to P
            var sq_line_length = math.lengthsq(line_segment);
            var frac = math.dot(line_segment, p_sub_a);
            frac = math.max(frac, 0.0f);                //Check if P projection is over vectorAB 
            frac = math.min(frac, sq_line_length);      //Check if P projection is over vectorAB 

            frac = frac / sq_line_length;              //The normalized "distance" from a to your closest point
            var nearest_point = a + line_segment * frac;

            var nearest_vector = nearest_point - point;
            signedDistance.cross = cross2D(nearest_vector, line_segment);

            /* assign the output */
            signedDistance.sign = signedDistance.cross < 0 ? 1 : -1;
            if (SDFCommon.USE_SQUARED_DISTANCES)
                signedDistance.distance = math.lengthsq(nearest_vector);
            else
                signedDistance.distance = math.length(nearest_vector);

            if (!EqualsLargeValues(frac, 0) && !EqualsLargeValues(frac, 1))
                signedDistance.cross = 1;
            else
            {
                line_segment = math.normalize(line_segment);
                nearest_vector = math.normalize(nearest_vector);
                signedDistance.cross = cross2D(line_segment, nearest_vector);
            }
            return true;
        }        
        static bool GetMinDistanceQuadraticNewton(SDFEdge quadratic, float2 point, ref SignedDistance signedDistance)
        {
            float min = int.MaxValue;           // shortest distance
            float min_factor = 0;               // factor at shortest distance
            float2 nearest_point = default;     // point on curve nearest to `point`

            var p0 = quadratic.start_pos;
            var p1 = quadratic.control1;
            var p2 = quadratic.end_pos;

            // compute substitution coefficients
            var aA = p0 - 2 * p1 + p2;
            var bB = 2 * (p1 - p0);
            var cC = p0;

            // do Newton's iterations
            for (int iterations = 0; iterations <= SDFCommon.MAX_NEWTON_DIVISIONS; iterations++)
            {
                float factor = (float)iterations / SDFCommon.MAX_NEWTON_DIVISIONS;

                for (int steps = 0; steps < SDFCommon.MAX_NEWTON_STEPS; steps++)
                {
                    var factor2 = factor * factor;
                    var curve_point = (aA * factor2) + (bB * factor) + cC; // B(t) = t^2 * A + t * B + p0                    
                    var dist_vector = curve_point - point;                // P(t) in the comment
                    var length = SDFCommon.USE_SQUARED_DISTANCES ? math.lengthsq(dist_vector) : math.length(dist_vector);
                    if (length < min)
                    {
                        min = length;
                        min_factor = factor;
                        nearest_point = curve_point;
                    }

                    /* This is Newton's approximation.          */
                    /*   t := P(t) . B'(t) /                    */
                    /*          (B'(t) . B'(t) + P(t) . B''(t)) */
                    var d1 = (aA * 2 * factor) + bB;                            // B'(t) = 2tA + B
                    var d2 = 2 * aA;                                            // B''(t) = 2A                   
                    var temp1 = math.dot(dist_vector, d1);                      // temp1 = P(t) . B'(t)
                    var temp2 = math.dot(d1, d1) + math.dot(dist_vector, d2);   // temp2 = B'(t) . B'(t) + P(t) . B''(t)
                    factor -= temp1 / temp2;

                    if (factor < 0 || factor > 1)
                        break;
                }
            }
            var direction = 2 * (aA * min_factor) + bB; // B'(t) = 2t * A + B

            // assign values, determine the sign
            var nearest_vector = nearest_point - point;
            signedDistance.cross = cross2D(nearest_vector, direction);
            signedDistance.distance = min;
            signedDistance.sign = signedDistance.cross < 0 ? 1 : -1;

            if (!EqualsLargeValues(min_factor, 0) && !EqualsLargeValues(min_factor, 1))
                signedDistance.cross = 1;   // the two are perpendicular
            else
            {
                /* compute `cross` if not perpendicular */
                direction = math.normalize(direction);
                nearest_point = math.normalize(nearest_vector);
                signedDistance.cross = cross2D(direction, nearest_vector);
            }
            return true;
        }
        static bool GetMinDistanceCubicNewton(SDFEdge cubic, float2 point, ref SignedDistance signedDistance)
        {
            float2 nearest_point = default;  // point on curve nearest to `point`
            float min_factor = 0;            // factor at shortest distance
            float min_factor_sq = 0;         // factor at shortest distance
            float min = int.MaxValue;        // shortest distance

            var p0 = cubic.start_pos;
            var p1 = cubic.control1;
            var p2 = cubic.control2;
            var p3 = cubic.end_pos;

            // compute substitution coefficients
            var aA = -p0 + 3 * (p1 - p2) + p3;
            var bB = 3 * (p0 - 2 * p1 + p2);
            var cC = 3 * (p1 - p0);
            var dD = p0;

            for (int iterations = 0; iterations <= SDFCommon.MAX_NEWTON_DIVISIONS; iterations++)
            {
                float factor = (float)iterations / SDFCommon.MAX_NEWTON_DIVISIONS;
                for (int steps = 0; steps < SDFCommon.MAX_NEWTON_STEPS; steps++)
                {
                    var factor2 = factor * factor;
                    var factor3 = factor2 * factor;
                    var curve_point = aA * factor3 + bB * factor2 + cC * factor + dD; // B(t) = t^3 * A + t^2 * B + t * C + D
                    var dist_vector = curve_point - point;                              // P(t) in the comment
                    var length = SDFCommon.USE_SQUARED_DISTANCES ? math.lengthsq(dist_vector) : math.length(dist_vector);
                    if (length < min)
                    {
                        min = length;
                        min_factor = factor;
                        min_factor_sq = factor2;
                        nearest_point = curve_point;
                    }

                    /* This the Newton's approximation.         */
                    /*   t := P(t) . B'(t) /                    */
                    /*          (B'(t) . B'(t) + P(t) . B''(t)) */
                    var d1 = aA * 3 * factor2 + bB * 2 * factor + cC;           // B'(t) = 3t^2 * A + 2t * B + C
                    var d2 = aA * 6 * factor + 2 * bB;                          // B''(t) = 6t * A + 2B
                    var temp1 = math.dot(dist_vector, d1);                      // temp1 = P(t) . B'(t)                  
                    var temp2 = math.dot(d1, d1) + math.dot(dist_vector, d2);   // temp2 = B'(t) . B'(t) + P(t) . B''(t)

                    factor -= temp1 / temp2;

                    if (factor < 0 || factor > 1)
                        break;
                }
            }
            var direction = aA * 3 * min_factor_sq + bB * 2 * min_factor + cC;  // B'(t) = 3t^2 * A + 2t * B + C

            // assign values, determine the sign
            var nearest_vector = nearest_point - point;
            signedDistance.cross = cross2D(nearest_vector, direction);
            signedDistance.distance = min;
            signedDistance.sign = signedDistance.cross < 0 ? 1 : -1;
            if (!EqualsLargeValues(min_factor, 0) && !EqualsLargeValues(min_factor, 1))
                signedDistance.cross = 1;   // the two are perpendicular
            else
            {
                /* compute `cross` if not perpendicular */
                direction = math.normalize(direction);
                nearest_point = math.normalize(nearest_vector);
                signedDistance.cross = cross2D(direction, nearest_vector);
            }
            return true;
        }
        public static SignedDistance ResolveCorner(SignedDistance sdf1, SignedDistance sdf2)
        {
            return math.abs(sdf1.cross) > math.abs(sdf2.cross) ? sdf1 : sdf2;
        }
        const float absolutTolerane = 0.000000001f;
        const float relativeTolerance = 0.000000001f;

        /// <summary>Tolerance comparison for large and small values. https://realtimecollisiondetection.net/blog/?p=89</summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(float a, float b)
        {
            return (math.abs(a - b) <= math.max(absolutTolerane, relativeTolerance * math.max(math.abs(a), math.abs(b))));
        }

        /// <summary>Relative tolerance comparison of x and y, fails values become small </summary>
        public static bool EqualsSmallValues(float x, float y)
        {
            return math.abs(x - y) <= relativeTolerance * math.max(math.abs(x), math.abs(y));
        }
        /// <summary>Absolute tolerance comparison of x and y, fails values become large  </summary>
        public static bool EqualsLargeValues(float x, float y)
        {
            return (math.abs(x - y) <= absolutTolerane);
        }

        /// <summary>Finds the magnitude of the cross product of two vectors (if we pretend they're in three dimensions) </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The magnitude of the cross product</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float cross2D(float2 a, float2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }
    }
}