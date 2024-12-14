using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.VisualScripting.YamlDotNet.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using static Unity.Burst.Intrinsics.X86;

namespace HarfBuzz.SDF
{
    public static class SDF
    {
        public static SignedDistance max_sdf => new SignedDistance { distance = int.MaxValue, sign = 0, cross = 0 };

        public const float CORNER_CHECK_EPSILON = 32 / (1 << 16); //The epsilon distance  used for corner

        public static bool SDFGenerateSubDivision(ref BezierData bezierData, SDFOrientation orientation, int spread, NativeArray<byte> buffer, GlyphRect glyphRect, int atlasWidth, int atlasHeight)
        {
            var success = true;
            if (bezierData.contourIDs.Length < 2 || bezierData.edges.Length == 0)
                return false;
            //var new_edges = new NativeList<SDFEdge>(edge_list.Length, Allocator.Temp);
            //SDFCommon.WriteGlyphOutlineToFile("BeforeSplit.txt", bezierData);
            success = SplitSDFShape(ref bezierData);
            //SDFCommon.WriteGlyphOutlineToFile("AfterSplit.txt", bezierData, true);
            success = SDFGenerateBoundingBox(ref bezierData, orientation, spread, buffer, glyphRect, atlasWidth, atlasHeight);
            return success;
        }
        public static bool SDFGenerate(ref BezierData bezierData, SDFOrientation orientation, float spread, NativeArray<byte> buffer, GlyphRect glyphRect, int atlasWidth, int atlasHeight)
        {
            var edges = bezierData.edges;
            var contourIDs = bezierData.contourIDs;

            bool flip_y = true;
            bool flip_sign = false;
            float sp_sq;   /* `spread` [* `spread`] as a 16.16 fixed value */
            SDFEdge edge;
            int targetIndex;

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;


            if (SDFCommon.USE_SQUARED_DISTANCES)
                sp_sq = spread * spread;
            else
                sp_sq = spread;

            if (atlasWidth == 0 || atlasHeight == 0)
            {
                Debug.Log($"sdf_generate:  Cannot render glyph with width/height == 0 (width: {atlasWidth}, height: {atlasHeight})");
                return false;
            }

            var rectX = glyphRect.x;
            var rectY = glyphRect.y;
            var rectWidth = glyphRect.width;
            var rectHeight = glyphRect.height;


            //var minDistances = new NativeArray<float>(buffer.Length, Allocator.Temp);
            /* loop over all rows */
            for (int row = 0; row < rectHeight; row++)
            {
                /* loop over all pixels of a row */
                for (int column = 0; column < rectWidth; column++)
                {
                    /* `grid_point` is the current pixel position; */
                    /* our task is to find the shortest distance   */
                    /* from this point to the entire shape.        */
                    float2 grid_point;
                    SignedDistance min_dist = max_sdf;
                    //int index;
                    float value;

                    grid_point.x = column;
                    grid_point.y = row;

                    /* This `grid_point' is at the corner, but we */
                    /* use the center of the pixel.               */
                    grid_point.x += 1f / 2f;
                    grid_point.y += 1f / 2f;

                    /* iterate over all contours manually */
                    for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
                    {
                        int startID = contourIDs[contourID];
                        int nextStartID = contourIDs[contourID + 1];

                        for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                        {
                            edge = edges[edgeID];
                            SignedDistance current_dist = max_sdf;

                            float diff;
                            SDFEdgeGetMinDistance(edge, grid_point, ref current_dist);

                            if (current_dist.distance >= 0)
                            {
                                diff = current_dist.distance - min_dist.distance;
                                if (math.abs(diff) < CORNER_CHECK_EPSILON)
                                    min_dist = ResolveCorner(min_dist, current_dist);
                                else if (diff < 0)
                                    min_dist = current_dist;
                            }
                            else
                                Debug.Log("sdf_contour_get_min_distance: Overflow.");

                            if (current_dist.distance < min_dist.distance)
                                min_dist = current_dist;
                        }
                    }

                    /* [OPTIMIZATION]: if (min_dist > sp_sq) then simply clamp  */
                    /*                 the value to spread to avoid square_root */
                    /* clamp the values to spread */
                    if (min_dist.distance > sp_sq)
                        min_dist.distance = sp_sq;

                    /* square_root the values and fit in a 6.10 fixed-point */
                    if (SDFCommon.USE_SQUARED_DISTANCES)
                        min_dist.distance = math.sqrt(min_dist.distance);

                    var minRaw = min_dist.distance;

                    if (orientation == SDFOrientation.FILL_LEFT)
                        min_dist.sign = -min_dist.sign;
                    if (flip_sign)
                        min_dist.sign = -min_dist.sign;

                    value = min_dist.distance;
                    value *= min_dist.sign;

                    if (flip_y)
                        targetIndex = (row + rectY) * atlasWidth + (column + rectX);
                    else
                        targetIndex = (atlasHeight - (row + rectY) - 1) * atlasWidth + (column + rectX);//this is not correct, needs review

                    var result = ((value + spread) * 16);
                    buffer[targetIndex] = (byte)(result);
                    //minDistances[index] = (float)min_dist.crossResult;
                }
            }
            //SDFCommon.WriteMinDistancesToFile("Distances.txt", minDistances);
            return true;
        }
        static bool SplitSDFShape(ref BezierData bezierData)
        {
            var edges = bezierData.edges;
            var contourIDs = bezierData.contourIDs;

            bool success = true;
            SDFEdge edge;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];

                //convert edgeList into linked list
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) 
                    edges.ElementAt(edgeID).nextId = edgeID + 1;
                edges.ElementAt(nextStartID - 1).nextId = -1;

                int iterator = startID;
                float dx;
                int num_splits, loopIT;
                do
                {
                    edge = edges[iterator];
                    switch (edge.edge_type)
                    {
                        case SDFEdgeType.LINE:
                            iterator = edge.nextId;
                            break;
                        case SDFEdgeType.QUADRATIC:
                            dx = GetDx(edges, iterator);
                            num_splits = 1;
                            while (dx > 0.25f)
                            {
                                dx /= 4;
                                num_splits *=2;
                            }
                            loopIT = iterator;
                            for (int i = 0; i < num_splits; i++)
                            {
                                iterator = split_conic(edges, loopIT);
                                loopIT = edges[loopIT].nextId;
                            }
                            break;
                        case SDFEdgeType.CUBIC:
                            dx = GetDx2(edges, iterator);
                            num_splits = 1;
                            while (dx > 0.25f)
                            {
                                dx /= 4;
                                num_splits *= 2;
                            }
                            loopIT = iterator;
                            for (int i = 0; i < num_splits; i++)
                            {
                                iterator = split_cubic(edges, loopIT);
                                loopIT = edges[loopIT].nextId;
                            }
                            break;

                        default:
                            iterator = edge.nextId;
                            break;                            
                            //error = FT_THROW(Invalid_Argument);
                    }                    
                } while (iterator != -1 && iterator!= nextStartID);
            }
            return success;
        }
        static float GetDx(NativeList<SDFEdge> edgeList, int edgeID)
        {
            var quadratic = edgeList[edgeID];
            var A = quadratic.start_pos;
            var B = quadratic.control1;
            var C = quadratic.end_pos;
            var deviation = math.abs(C + A - 2 * B);

            if (deviation.x < deviation.y)
                return deviation.y;
            return deviation.x;
        }

        static float GetDx2(NativeList<SDFEdge> edgeList, int edgeID)
        {
            var quadratic = edgeList[edgeID];
            var start_pos = quadratic.start_pos;
            var control1 = quadratic.control1;
            var control2 = quadratic.control2;
            var end_pos = quadratic.end_pos;
            //var deviation = math.abs(end_pos + start_pos - 2 * control1);

            var dx1 = math.abs(start_pos - 3 * control2 + 2 * end_pos);
            var dx2 = math.abs(2 * start_pos - 3 * control1.x + control2);

            float maxDx1 = math.max(dx1.x, dx1.y);
            float maxDx2 = math.max(dx2.x, dx2.y);

            return math.max(maxDx1, maxDx2);
        }

        /// <summary> This function splits a quadratic bezier into two quadratic bezier exactly half way at t = 0.5. </summary>
        static int split_conic(NativeList<SDFEdge> edgeList, int edgeID)
        {
            edgeList.Add(new SDFEdge());
            var newEdgeID = edgeList.Length - 1;
            ref var edge = ref edgeList.ElementAt(edgeID);
            ref var newEdge = ref edgeList.ElementAt(newEdgeID);
            var A = edge.start_pos;
            var B = edge.control1;
            var C = edge.end_pos;

            var D = (A + B) * 0.5f;
            var E = (B + C) * 0.5f;
            var F = (D + E) * 0.5f;

            edge.start_pos = A;
            edge.control1 = D;
            edge.end_pos = F;

            newEdge.start_pos = F;
            newEdge.control1 = E;
            newEdge.end_pos = C;

            newEdge.nextId = edge.nextId;
            edge.nextId = newEdgeID;
            edge.edge_type = SDFEdgeType.LINE;
            newEdge.edge_type = SDFEdgeType.LINE;
            return newEdge.nextId;
        }
        /// <summary> This function splits a cubic bezier into two cubic bezier exactly half way at t = 0.5. </summary>
        static int split_cubic(NativeList<SDFEdge> edgeList, int edgeID)
        {
            edgeList.Add(new SDFEdge());
            var newEdgeID = edgeList.Length - 1;
            ref var edge = ref edgeList.ElementAt(edgeID);
            ref var newEdge = ref edgeList.ElementAt(newEdgeID);
            var A = edge.start_pos;
            var B = edge.control1;
            var C = edge.control2;
            var D = edge.end_pos;

            var E = (A + B) * 0.5f;
            var F = (B + C) * 0.5f;
            var G = (C + D) * 0.5f;
            var H = (E + F) * 0.5f;
            var J = (F + G) * 0.5f;
            var K = (H + J) * 0.5f;

            edge.start_pos = A;
            edge.control1 = E;
            edge.control2 = H;
            edge.end_pos = K;

            newEdge.start_pos = K;
            newEdge.control1 = J;
            newEdge.control2 = G;
            newEdge.end_pos = D;

            newEdge.nextId = edge.nextId;
            edge.nextId = newEdgeID;
            edge.edge_type = SDFEdgeType.LINE;
            newEdge.edge_type = SDFEdgeType.LINE;
            return newEdge.nextId;
        }
        static bool SDFGenerateBoundingBox(ref BezierData bezierData, SDFOrientation orientation, int spread, NativeArray<byte> buffer, GlyphRect glyphRect, int atlasWidth, int atlasHeight)
        {
            var edges = bezierData.edges;
            var contourIDs = bezierData.contourIDs;

            bool flip_y = true;
            bool flip_sign = false;
            int overloadSign = 0;
            float sp_sq;   /* `spread` [* `spread`] as a 16.16 fixed value */
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

                    /* get the control box and increase it by `spread' */
                    cbox = GetControlBox(edge);
                    //cbox = GetBBox(edge);
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

                            /* This `grid_point` is at the corner, but we */
                            /* use the center of the pixel.               */
                            grid_point.x += 1f / 2f;
                            grid_point.y += 1f / 2f;
                            SDFEdgeGetMinDistance(edge, grid_point, ref dist);

                            if (orientation == SDFOrientation.FILL_LEFT)
                                dist.sign = -dist.sign;

                            /* ignore if the distance is greater than spread;       */
                            /* otherwise it creates artifacts due to the wrong sign */
                            if (dist.distance > sp_sq)
                                continue;

                            /* take the square root of the distance if required */
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

                            /* check whether the pixel is set or not */
                            if (Equals(dists[index].sign, 0))
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

            /* final pass */
            //var minDistances = new NativeArray<float>(buffer.Length, Allocator.Temp);
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

                    /* if the pixel is not set                     */
                    /* its shortest distance is more than `spread` */
                    var dist = dists[sourceIndex];
                    if (Equals(dist.sign, 0))
                    {
                        dist.sign = outsideSign;
                        dist.distance = -spread;
                    }
                    current_sign = dist.sign;

                    /* clamp the values */
                    if (dist.distance > spread)
                        dist.distance = spread;

                    /* flip sign if required */
                    dist.distance *= flip_sign ? -current_sign : current_sign;
                    dists[sourceIndex] = dist;

                    /* concatenate to appropriate format */
                    var result = ((dist.distance + spread) * 16);
                    buffer[targetIndex] = (byte)result;
                    //minDistances[sourceIndex] = result;
                }
            }
            //SDFCommon.WriteMinDistancesToFile("DistancesSubDivide.txt", minDistances);
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
                    //Debug.Log("line");
                    success = GetMinDistanceLine(edge, point, ref signedDistance);
                    break;
                case SDFEdgeType.QUADRATIC:
                    //Debug.Log("quadratic");
                    success = GetMinDistanceQuadraticNewton(edge, point, ref signedDistance);
                    break;
                case SDFEdgeType.CUBIC:
                    //Debug.Log("cubic");
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

            /* Instead of finding `cross` for checking corner we */
            /* directly set it here.  This is more efficient     */
            /* because if the distance is perpendicular we can   */
            /* directly set it to 1.                             */
            //if (factor != 0 && factor != 1)
            if (!Equals(frac, 0) && !Equals(frac - 1, 0))
                signedDistance.cross = 1;
            else
            {
                /* [OPTIMIZATION]: Pre-compute this direction. */
                /* If not perpendicular then compute `cross`.  */
                line_segment = math.normalize(line_segment);
                nearest_vector = math.normalize(nearest_vector);
                signedDistance.cross = cross2D(line_segment, nearest_vector);
            }
            return true;
        }
        static bool GetMinDistanceQuadratic(SDFEdge quadratic, float2 point, ref SignedDistance signedDistance)
        {
            float min = int.MaxValue;       // shortest distance
            float min_factor = 0;           // factor at shortest distance
            float2 nearest_point = default;   // point on curve nearest to `point`
            float3 roots;

            var p0 = quadratic.start_pos;
            var p1 = quadratic.control1;
            var p2 = quadratic.end_pos;

            /* compute substitution coefficients */
            var aA = p0 - 2 * p1 + p2;
            var bB = 2 * (p1 - p0);
            var cC = p0;

            /* compute cubic coefficients */
            var a = math.dot(aA, aA);
            var b = 3 * math.dot(aA, bB);
            var c = 2 * math.dot(bB, bB) + math.dot(aA, p0) - math.dot(aA, point);
            var d = math.dot(p0, bB) - math.dot(point, bB);

            /* find the roots */
            var num_roots = solve_cubic_equation(a, b, c, d, out roots);

            if (num_roots == 0)
            {
                roots[0] = 0;
                roots[1] = 1;
                num_roots = 2;
            }
            /* [OPTIMIZATION]: Check the roots, clamp them and discard */
            /*                 duplicate roots.                        */

            //Debug.Log($"{num_roots} roots: {roots[0]} {roots[1]} {roots[2]}");
            for (int i = 0; i < num_roots; i++)
            {
                var t = roots[i];
                var t2 = 0f;
                var dist = 0f;

                /*
                 * Ideally we should discard the roots which are outside the range
                 * [0.0, 1.0] and check the endpoints of the Bezier curve, but Behdad
                 * Esfahbod proved the following lemma.
                 *
                 * Lemma:
                 *
                 * (1) If the closest point on the curve [0, 1] is to the endpoint at
                 *     `t` = 1 and the cubic has no real roots at `t` = 1 then the
                 *     cubic must have a real root at some `t` > 1.
                 *
                 * (2) Similarly, if the closest point on the curve [0, 1] is to the
                 *     endpoint at `t` = 0 and the cubic has no real roots at `t` = 0
                 *     then the cubic must have a real root at some `t` < 0.
                 *
                 * Now because of this lemma we only need to clamp the roots and that
                 * will take care of the endpoints.
                 *
                 * For more details see
                 *
                 *   https://lists.nongnu.org/archive/html/freetype-devel/2020-06/msg00147.html
                 */

                if (t < 0)
                    t = 0;
                if (t > 1)
                    t = 1;

                t2 = t * t;

                /* B(t) = t^2 * A + 2t * B + p0 - p */
                var curve_point = (aA * t2) + 2 * (bB * t) + p0;

                /* `curve_point` - `p` */
                var dist_vector = curve_point - point;
                dist = SDFCommon.USE_SQUARED_DISTANCES ? math.lengthsq(dist_vector) : math.length(dist_vector);
                if (dist < min)
                {
                    min = dist;
                    nearest_point = curve_point;
                    min_factor = t;
                }
            }
            var direction = 2 * (aA * min_factor) + 2 * bB; /* B'(t) = 2 * (tA + B) */

            /* determine the sign */
            var nearest_vector = nearest_point - point;
            signedDistance.cross = cross2D(nearest_vector, direction);

            /* assign the values */
            signedDistance.distance = min;
            signedDistance.sign = signedDistance.cross < 0 ? 1 : -1;

            if (min_factor != 0 && min_factor != 1)
                signedDistance.cross = 1;   /* the two are perpendicular */
            else
            {
                /* compute `cross` if not perpendicular */
                direction = math.normalize(direction);
                nearest_point = math.normalize(nearest_vector);
                signedDistance.cross = cross2D(direction, nearest_vector);
            }

            return true;
        }
        static bool GetMinDistanceQuadraticNewton(SDFEdge quadratic, float2 point, ref SignedDistance signedDistance)
        {
            float min = int.MaxValue;       // shortest distance
            float min_factor = 0;           // factor at shortest distance
            float2 nearest_point = default;   // point on curve nearest to `point`

            var p0 = quadratic.start_pos;
            var p1 = quadratic.control1;
            var p2 = quadratic.end_pos;

            /* compute substitution coefficients */
            var aA = p0 - 2 * p1 + p2;
            var bB = 2 * (p1 - p0);
            var cC = p0;

            /* do Newton's iterations */
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

            /* determine the sign */
            var nearest_vector = nearest_point - point;
            signedDistance.cross = cross2D(nearest_vector, direction);

            /* assign the values */
            signedDistance.distance = min;
            signedDistance.sign = signedDistance.cross < 0 ? 1 : -1;

            if (!Equals(min_factor, 0) && !Equals(min_factor - 1, 0))
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
            float2 nearest_point = default;  /* point on curve nearest to `point`     */
            float min_factor = 0;            /* factor at shortest distance */
            float min_factor_sq = 0;            /* factor at shortest distance */
            float min = int.MaxValue;   /* shortest distance          */

            var p0 = cubic.start_pos;
            var p1 = cubic.control1;
            var p2 = cubic.control2;
            var p3 = cubic.end_pos;

            /* compute substitution coefficients */
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

            /* determine the sign */
            var nearest_vector = nearest_point - point;
            signedDistance.cross = cross2D(nearest_vector, direction);

            //* assign the values */
            signedDistance.distance = min;
            signedDistance.sign = signedDistance.cross < 0 ? 1 : -1;
            if (!Equals(min_factor, 0) && !Equals(min_factor - 1, 0))
                signedDistance.cross = 1;   /* the two are perpendicular */
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
        const float absTol = 0.000000001f;
        const float relTol = 0.000000001f;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(float a, float b)
        {
            return (math.abs(a - b) <= math.max(absTol, relTol * math.max(math.abs(a), math.abs(b))));
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
        static int solve_cubic_equation(float a, float b, float c, float d, out float3 roots3)
        {
            roots3 = float3.zero;
            float q = 0;      /* intermediate */
            float r = 0;      /* intermediate */

            float a2 = b;     /* x^2 coefficients */
            float a1 = c;     /* x coefficients   */
            float a0 = d;     /* constant         */

            float q3 = 0;
            float r2 = 0;
            float a23 = 0;
            float a22 = 0;
            float a1x2 = 0;


            var roots2 = float2.zero;
            /* cutoff value for `a` to be a cubic, otherwise solve quadratic */
            if (a == 0 || math.abs(a) < (16 / (1 << 6)))
                return solve_quadratic_equation(b, c, d, out roots2);

            if (d == 0)
            {
                roots3[0] = 0;

                var numRoots = solve_quadratic_equation(a, b, c, out roots2) + 1;
                roots3[1] = roots2[0];
                roots3[2] = roots2[1];
                return numRoots;
            }

            /* normalize the coefficients; this also makes them 16.16 */
            a2 = a2 / a;
            a1 = a1 / a;
            a0 = a0 / a;

            /* compute intermediates */
            a1x2 = a1 * a2;
            a22 = a2 * a2;
            a23 = a22 * a2;

            q = (3 * a1 - a22) / 9;
            r = (9 * a1x2 - 27 * a0 - 2 * a23) / 54;

            /* [BUG]: `q3` and `r2` still cause underflow. */

            q3 = q * q;
            q3 = q3 * q;

            r2 = r * r;

            if (q3 < 0 && r2 < -q3)
            {
                float t = 0f;

                q3 = math.sqrt(-q3);
                t = r / q3;

                if (t > 1)
                    t = 1;
                if (t < -1)
                    t = -1;

                t = math.acos(t);

                a2 /= 3;
                q = 2 * math.sqrt(-q);

                roots3[0] = q * math.cos(t / 3) - a2;
                roots3[1] = q * math.cos((t + math.PI * 2) / 3) - a2;
                roots3[2] = q * math.cos((t + math.PI * 4) / 3) - a2;

                return 3;
            }
            else if (Equals(r2 - -q3,0))//if (r2 == -q3)
            {
                float s = 0f;

                s = math.pow(r, 1f / 3f);
                a2 /= -3;

                roots3[0] = a2 + (2 * s);
                roots3[1] = a2 - s;

                return 2;
            }

            else
            {
                float s = 0f;
                float t = 0f;
                float dis = 0f;

                if (q3 == 0)
                    dis = math.abs(r);
                else
                    dis = math.sqrt(q3 + r2);

                s = math.pow(r + dis, 1f / 3f);
                t = math.pow(math.abs(r - dis), 1f / 3f);
                a2 /= -3;
                roots3[0] = (a2 + (s + t));
                return 1;
            }
        }

        static int solve_quadratic_equation(float a, float b, float c, out float2 roots)
        {
            roots = float2.zero;
            if (a == 0)
            {
                if (b == 0)
                    return 0;
                else
                {
                    roots[0] = -c / b;
                    return 1;
                }
            }

            var discriminant = (b * b) - 4 * (a * c);

            if (discriminant < 0)
                return 0;
            else if (discriminant == 0)
            {
                roots[0] = -b / 2 * a;
                return 1;
            }
            else
            {
                discriminant = math.sqrt(discriminant);

                roots[0] = -b + discriminant / 2 * a;
                roots[1] = -b - discriminant / 2 * a;

                return 2;
            }

        }
    }
}