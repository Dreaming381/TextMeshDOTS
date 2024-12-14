using FixPointCS;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HarfBuzz.SDF
{
    public static class SDFFixedPoint
    {
        public static SignedDistanceFixed max_sdf => new SignedDistanceFixed { distance = int.MaxValue, sign = 0, cross = 0 };
        public const int CORNER_CHECK_EPSILON = 32;//The epsilon distance (in 16.16 fractional units) used for corner
        public static bool SDFGenerateSubDivision(ref BezierData bezierData, int spread, NativeArray<byte> buffer, RectInt glyphRect, int atlasWidth, int atlasHeight)
        {
            var success = true;
            if (bezierData.contourIDs.Length < 2 || bezierData.edges.Length == 0)
                return false;
            //var new_edges = new NativeList<SDFEdge>(edge_list.Length, Allocator.Temp);
            //success = SplitSDFShape(edge_list, new_edges);
            success = SDFGenerateBoundingBox(ref bezierData, spread, buffer, glyphRect, atlasWidth, atlasHeight);
            return success;
        }
        public static bool SDFGenerate(NativeList<SDFEdge> shape, int spread, NativeArray<byte> buffer, int width, int rows)
        {
            bool flip_y = true;
            SDFOrientation orientation = SDFOrientation.TRUETYPE;
            bool flip_sign = true;
            int sp_sq;   /* `spread` [* `spread`] as a 16.16 fixed value */

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;


            if (SDFCommon.USE_SQUARED_DISTANCES)
                sp_sq = FT_INT_16D16(spread * spread);
            else
                sp_sq = FT_INT_16D16(spread);

            if (width == 0 || rows == 0)
            {
                Debug.Log($"sdf_generate:  Cannot render glyph with width/height == 0 (width: {width}, height: {rows})");
                return false;
            }
            var minDistances = new NativeArray<float>(buffer.Length, Allocator.Temp);
            /* loop over all rows */
            for (int y = 0; y < rows; y++)
            {
                /* loop over all pixels of a row */
                for (int x = 0; x < width; x++)
                {
                    /* `grid_point` is the current pixel position; */
                    /* our task is to find the shortest distance   */
                    /* from this point to the entire shape.        */
                    int2 grid_point; //FT_26D6_Vec
                    SignedDistanceFixed min_dist = max_sdf;

                    int index;
                    grid_point.x = FT_INT_26D6(x);
                    grid_point.y = FT_INT_26D6(y);
                    /* This `grid_point' is at the corner, but we */
                    /* use the center of the pixel.               */
                    grid_point.x += FT_INT_26D6(1) / 2;
                    grid_point.y += FT_INT_26D6(1) / 2;
                    //contour_list = shape->contours;
                    /* iterate over all contours manually */
                    //for (int i = 0, length=shape.Length; i < length; i++)
                    //{
                    SignedDistanceFixed current_dist = max_sdf;
                    SDFContourGetMinDistance(shape, grid_point, ref current_dist);

                    if (current_dist.distance < min_dist.distance)
                        min_dist = current_dist;

                    //contour_list = contour_list->next;
                    //}

                    /* [OPTIMIZATION]: if (min_dist > sp_sq) then simply clamp  */
                    /*                 the value to spread to avoid square_root */

                    /* clamp the values to spread */
                    if (min_dist.distance > sp_sq)
                        min_dist.distance = sp_sq;

                    /* square_root the values and fit in a 6.10 fixed-point */
                    if (SDFCommon.USE_SQUARED_DISTANCES)
                        min_dist.distance = SquareRoot(min_dist.distance);

                    if (orientation == SDFOrientation.FILL_LEFT)
                        min_dist.sign = (sbyte)-min_dist.sign;
                    if (flip_sign)
                        min_dist.sign = (sbyte)-min_dist.sign;
                    min_dist.distance /= 65536; /* convert from 16.16 to 22.10 */
                    //value = min_dist.distance & 0x0000FFFF; /* truncate to 6.10 */

                    //min_dist.distance /= 65536;
                    //value = min_dist.distance & 0x000000FF;
                    min_dist.distance *= min_dist.sign;
                    if (flip_y)
                        index = y * width + x;
                    else
                        index = (rows - y - 1) * width + x;

                    //var result = ((value + spread) * 24);
                    MapFixedToSDF(min_dist.distance, spread, out byte result);

                    buffer[index] = (byte)result;
                    minDistances[index] = result;
                }
            }
            //SDFCommon.WriteMinDistancesToFile("DistancesFixed.txt", minDistances);
            return true;
        }

        static bool SDFGenerateBoundingBox(ref BezierData bezierData, int spread, NativeArray<byte> buffer, RectInt glyphRect, int atlasWidth, int atlasHeight)
        {
            var edges = bezierData.edges;
            var contourIDs = bezierData.contourIDs;

            bool flip_y = true;
            SDFOrientation orientation = SDFOrientation.TRUETYPE;
            bool flip_sign = true;
            int overloadSign = 0;
            int sp_sq;   /* `spread` [* `spread`] as a 16.16 fixed value */
            var dists = new NativeArray<SignedDistanceFixed>(glyphRect.width * glyphRect.height, Allocator.Temp);

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;

            int fixed_spread = FT_INT_16D16(spread);

            if (SDFCommon.USE_SQUARED_DISTANCES)
                sp_sq = FT_INT_16D16(spread * spread);
            else
                sp_sq = FT_INT_16D16(spread);

            int maxIndex = int.MinValue;
            int minIndex = int.MaxValue;
            var rectX = glyphRect.x;
            var rectY = glyphRect.y;
            var rectWidth = glyphRect.width;
            var rectHeight = glyphRect.height;

            SDFEdge edge;
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
                            int2 grid_point;
                            SignedDistanceFixed dist = max_sdf;
                            int index = 0;
                            int diff = 0;

                            if (x < 0 || x >= rectWidth)
                                continue;
                            if (y < 0 || y >= rectHeight)
                                continue;

                            grid_point.x = FT_INT_26D6(x);
                            grid_point.y = FT_INT_26D6(y);

                            /* This `grid_point` is at the corner, but we */
                            /* use the center of the pixel.               */
                            grid_point.x += FT_INT_26D6(1) / 2;
                            grid_point.y += FT_INT_26D6(1) / 2;

                            SDFEdgeGetMinDistance(edge, grid_point, ref dist);                            

                            if (orientation == SDFOrientation.FILL_LEFT)
                                dist.sign = (sbyte)-dist.sign;

                            /* ignore if the distance is greater than spread;       */
                            /* otherwise it creates artifacts due to the wrong sign */
                            if (dist.distance > sp_sq)
                                continue;

                            /* take the square root of the distance if required */
                            if (SDFCommon.USE_SQUARED_DISTANCES)
                                dist.distance = SquareRoot(dist.distance);

                            if (flip_y)
                                index = y * rectWidth + x;
                            else
                                index = (rectHeight - y - 1) * rectWidth + x;

                            if (index < minIndex)
                                minIndex = index;
                            if (index > maxIndex)
                                maxIndex = index;

                            /* check whether the pixel is set or not */
                            if (dists[index].sign == 0)
                                dists[index] = dist;
                            else
                            {
                                diff = FT_ABS(dists[index].distance - dist.distance);

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
            var minDistances = new NativeArray<float>(buffer.Length, Allocator.Temp);
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
                    minDistances[sourceIndex] = dist.distance;
                    if (dist.sign == 0)
                    {
                        dist.sign = outsideSign;
                        dist.distance = -fixed_spread;
                    }
                    current_sign = dist.sign;

                    /* clamp the values */
                    if (dist.distance > fixed_spread)
                        dist.distance = fixed_spread;

                    /* flip sign if required */
                    dist.distance *= flip_sign ? -current_sign : current_sign;
                    dists[sourceIndex] = dist;

                    /* concatenate to appropriate format */
                    MapFixedToSDF(dist.distance, fixed_spread, out byte result);
                    buffer[targetIndex] = result;
                    minDistances[sourceIndex] = result;
                }
            }
            SDFCommon.WriteMinDistancesToFile("DistancesFixedSubDivide.txt", minDistances);
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
        /// <summary>
        /// Converts a FP value into a float.
        /// </summary>
        [MethodImpl(FixedUtil.AggressiveInlining)]
        public static float ToFloat(int v)
        {
            return (float)v * (1.0f / (1 << 6));
        }
        public static bool SDFContourGetMinDistance(NativeList<SDFEdge> edge_list, int2 point, ref SignedDistanceFixed signedDistance)
        {
            SignedDistanceFixed min_dist = max_sdf;

            /* iterate over all the edges manually */
            for (int i = 0, length = edge_list.Length; i < length; i++)
            {
                var edge = edge_list[i];
                SignedDistanceFixed current_dist = max_sdf;
                int diff;
                SDFEdgeGetMinDistance(edge, point, ref current_dist);

                if (current_dist.distance >= 0)
                {
                    diff = current_dist.distance - min_dist.distance;
                    if (FT_ABS(diff) < CORNER_CHECK_EPSILON)
                        min_dist = resolve_corner(min_dist, current_dist);
                    else if (diff < 0)
                        min_dist = current_dist;
                }
                else
                    Debug.Log("sdf_contour_get_min_distance: Overflow.");
            }
            signedDistance = min_dist;
            return true;
        }
        public static bool SDFEdgeGetMinDistance(SDFEdge edge, int2 point, ref SignedDistanceFixed signedDistance)
        {
            bool success = false;
            switch (edge.edge_type)
            {
                case SDFEdgeType.LINE:
                    success = GetMinDistanceLineFixed(edge, point, ref signedDistance);
                    break;
                case SDFEdgeType.QUADRATIC:
                    Debug.Log($"QUADRATIC Not implemented");
                    //success = GetMinDistanceCubic(edge, point, out signedDistance);
                    break;
                case SDFEdgeType.CUBIC:
                    success = GetMinDistanceCubicFixed(edge, point, ref signedDistance);
                    break;
                default:
                    break;
            }
            return success;
        }
        static bool GetMinDistanceLineFixed(SDFEdge line, int2 point, ref SignedDistanceFixed signedDistance)
        {
            int2 a;                   /* start position */
            int2 b;                   /* end position   */
            int2 p;                   /* current point  */

            int2 line_segment;      /* `b` - `a` */
            int2 p_sub_a;           /* `p` - `a` */

            int sq_line_length;       /* squared length of `line_segment` */
            int factor;               /* factor of the nearest point      */
            int cross;                /* used to determine sign           */

            int2 nearest_point;    /* `point_on_line`       */
            int2 nearest_vector;   /* `p` - `nearest_point` */


            if (line.edge_type != SDFEdgeType.LINE)
            {
                Debug.Log($"Edge is not a line: {line.edge_type}");
                return false;
            }

            a = FT_26D6VecFromFloat(line.start_pos);
            b = FT_26D6VecFromFloat(line.end_pos);
            p = point;

            line_segment.x = b.x - a.x;
            line_segment.y = b.y - a.y;

            p_sub_a.x = p.x - a.x;
            p_sub_a.y = p.y - a.y;

            sq_line_length = (line_segment.x * line_segment.x) +
                             (line_segment.y * line_segment.y);

            factor = (p_sub_a.x * line_segment.x) +
                     (p_sub_a.y * line_segment.y);

            factor = factor / sq_line_length;

            /* clamp the factor between 0.0 and 1.0 in fixed-point */
            if (factor > 1)
                factor = 1;
            if (factor < 0)
                factor = 0;

            nearest_point.x = line_segment.x * factor;
            nearest_point.y = line_segment.y * factor;

            nearest_point.x = a.x + nearest_point.x;
            nearest_point.y = a.y + nearest_point.y;

            nearest_vector.x = nearest_point.x - p.x;
            nearest_vector.y = nearest_point.y - p.y;

            cross = (nearest_vector.x * line_segment.y) -
                    (nearest_vector.y * line_segment.x);

            /* assign the output */
            signedDistance.sign = cross < 0 ? 1 : -1;
            signedDistance.distance = VECTOR_LENGTH_16D16(nearest_vector); 

            /* Instead of finding `cross` for checking corner we */
            /* directly set it here.  This is more efficient     */
            /* because if the distance is perpendicular we can   */
            /* directly set it to 1.                             */
            //if (factor != 0 && factor != 1)
            if (factor != 0 && factor != FT_INT_16D16(1))
                signedDistance.cross = FT_INT_16D16(1);
            else
            {
                /* [OPTIMIZATION]: Pre-compute this direction. */
                /* If not perpendicular then compute `cross`.  */
                //FT_Vector_NormLen(line_segment);
                //FT_Vector_NormLen(nearest_vector);

                var tmpLine_segment = ToDoubleVec(line_segment);
                tmpLine_segment = math.normalize(tmpLine_segment);
                line_segment = FT_16D16VecFromDouble(tmpLine_segment);

                var tmpNearest_vector = ToDoubleVec(nearest_vector);
                tmpNearest_vector = math.normalize(tmpNearest_vector);
                nearest_vector = FT_16D16VecFromDouble(tmpNearest_vector);

                signedDistance.cross = (line_segment.x * nearest_vector.y) -
                                       (line_segment.y * nearest_vector.x);
            }
            return true;
        }
        public static bool GetMinDistanceCubicFixed(SDFEdge cubic, int2 point, ref SignedDistanceFixed signedDistance)
        {
            int2 aA, bB, cC, dD;            // FT_26D6_Vec A, B, C, D in the above comment
            int2 nearest_point = default;   // FT_16D16_Vec  point on curve nearest to `point`
            int2 direction;                 // FT_16D16_Vec direction of curve at `nearest_point`

            int2 p0, p1, p2, p3;            // FT_26D6_Vec control points of a cubic curve
            int2 p;                         // `FT_26D6_Vec point` to which shortest distance

            int min_factor = 0;             // FT_16D16 factor at shortest distance
            int min_factor_sq = 0;          // FT_16D16 factor at shortest distance
            int cross;                      // FT_16D16 to determine the sign
            int min = int.MaxValue;         // FT_16D16 shortest distance

            ushort iterations;
            ushort steps;

            if (cubic.edge_type != SDFEdgeType.CUBIC)
            {
                Debug.Log($"Edge is not cubic: {cubic.edge_type}");
                return false;
            }

            p0 = FT_26D6VecFromFloat(cubic.start_pos);
            p1 = FT_26D6VecFromFloat(cubic.control1);
            p2 = FT_26D6VecFromFloat(cubic.control2);
            p3 = FT_26D6VecFromFloat(cubic.end_pos);
            p = point;

            /* compute substitution coefficients */
            aA.x = -p0.x + 3 * (p1.x - p2.x) + p3.x;
            aA.y = -p0.y + 3 * (p1.y - p2.y) + p3.y;

            bB.x = 3 * (p0.x - 2 * p1.x + p2.x);
            bB.y = 3 * (p0.y - 2 * p1.y + p2.y);

            cC.x = 3 * (p1.x - p0.x);
            cC.y = 3 * (p1.y - p0.y);

            dD.x = p0.x;
            dD.y = p0.y;

            for (iterations = 0; iterations <= SDFCommon.MAX_NEWTON_DIVISIONS; iterations++)
            {
                int factor = FT_INT_16D16(iterations) / SDFCommon.MAX_NEWTON_DIVISIONS; //FT_16D16

                int factor2;         //FT_16D16 factor^2
                int factor3;         //FT_16D16 factor^3
                int length;         //FT_16D16

                int2 curve_point; //FT_16D16_Vec point on the curve
                int2 dist_vector; //FT_16D16_Vec `curve_point' - `p'

                int2 d1;           // FT_26D6_Vec first  derivative
                int2 d2;           //FT_26D6_Vec second derivative

                int temp1; //FT_16D16
                int temp2; //FT_16D16


                for (steps = 0; steps < SDFCommon.MAX_NEWTON_STEPS; steps++)
                {
                    factor2 = Fixed32.Mul(factor, factor);
                    factor3 = Fixed32.Mul(factor2, factor);

                    /* B(t) = t^3 * A + t^2 * B + t * C + D */
                    curve_point.x = Fixed32.Mul(aA.x, factor3) +
                                    Fixed32.Mul(bB.x, factor2) +
                                    Fixed32.Mul(cC.x, factor) + dD.x;
                    curve_point.y = Fixed32.Mul(aA.y, factor3) +
                                    Fixed32.Mul(bB.y, factor2) +
                                    Fixed32.Mul(cC.y, factor) + dD.y;

                    /* convert to 16.16 */
                    curve_point.x = FT_26D6_16D16(curve_point.x);
                    curve_point.y = FT_26D6_16D16(curve_point.y);

                    /* P(t) in the comment */
                    dist_vector.x = curve_point.x - FT_26D6_16D16(p.x);
                    dist_vector.y = curve_point.y - FT_26D6_16D16(p.y);

                    length = VECTOR_LENGTH_16D16(dist_vector);

                    if (length < min)
                    {
                        min = length;
                        min_factor = factor;
                        min_factor_sq = factor2;
                        nearest_point = curve_point;
                    }

                    /* This the Newton's approximation.         */
                    /*                                          */
                    /*   t := P(t) . B'(t) /                    */
                    /*          (B'(t) . B'(t) + P(t) . B''(t)) */

                    /* B'(t) = 3t^2 * A + 2t * B + C */
                    d1.x = Fixed32.Mul(aA.x, 3 * factor2) +
                           Fixed32.Mul(bB.x, 2 * factor) + cC.x;
                    d1.y = Fixed32.Mul(aA.y, 3 * factor2) +
                           Fixed32.Mul(bB.y, 2 * factor) + cC.y;

                    /* B''(t) = 6t * A + 2B */
                    d2.x = Fixed32.Mul(aA.x, 6 * factor) + 2 * bB.x;
                    d2.y = Fixed32.Mul(aA.y, 6 * factor) + 2 * bB.y;

                    dist_vector.x /= 1024;
                    dist_vector.y /= 1024;

                    /* temp1 = P(t) . B'(t) */
                    temp1 = VEC_26D6_DOT(dist_vector, d1);

                    /* temp2 = B'(t) . B'(t) + P(t) . B''(t) */
                    temp2 = VEC_26D6_DOT(d1, d1) +
                            VEC_26D6_DOT(dist_vector, d2);

                    factor -= Fixed32.DivPrecise(temp1, temp2);

                    if (factor < 0 || factor > FT_INT_16D16(1))
                        break;
                }
            }
            /* B'(t) = 3t^2 * A + 2t * B + C */
            direction.x = Fixed32.Mul(aA.x, 3 * min_factor_sq) +
                          Fixed32.Mul(bB.x, 2 * min_factor) + cC.x;
            direction.y = Fixed32.Mul(aA.y, 3 * min_factor_sq) +
                          Fixed32.Mul(bB.y, 2 * min_factor) + cC.y;

            /* determine the sign */
            cross = Fixed32.Mul(nearest_point.x - FT_26D6_16D16(p.x), direction.y) -
                    Fixed32.Mul(nearest_point.y - FT_26D6_16D16(p.y), direction.x);

            /* assign the values */
            signedDistance.distance = min;
            signedDistance.sign = cross < 0 ? (sbyte)1 : (sbyte)-1;

            if (min_factor != 0 && min_factor != FT_INT_16D16(1))
                signedDistance.cross = 1;   /* the two are perpendicular */
            else
            {
                /* convert to nearest vector */
                nearest_point.x -= FT_26D6_16D16(p.x);
                nearest_point.y -= FT_26D6_16D16(p.y);

                /* compute `cross` if not perpendicular */
                //FT_Vector_NormLen(&direction);
                //FT_Vector_NormLen(&nearest_point);
                var tmpDirection = ToDoubleVec(direction);
                tmpDirection = math.normalize(tmpDirection);
                direction = FT_16D16VecFromDouble(tmpDirection);

                var tmpNearest_point = ToDoubleVec(nearest_point);
                tmpNearest_point = math.normalize(tmpNearest_point);
                nearest_point = FT_16D16VecFromDouble(tmpNearest_point);

                signedDistance.cross = Fixed32.Mul(direction.x, nearest_point.y) -
                                       Fixed32.Mul(direction.y, nearest_point.x);
            }
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FT_INT_26D6(int x) { return x * 64; }     // convert int to 26.6 fixed-point

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FT_26D6_16D16(int x) { return x * 1024; } // convert 26.6 to 16.16 fixed-point

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FT_INT_16D16(int x) { return x * 65536; }  //* convert int to 16.16 fixed-point

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 FT_16D16VecFromDouble(double2 v) { return (int2)(v * 65536.0); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 FT_26D6VecFromFloat(float2 v) { return (int2)(v * 64.0f); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 ToDoubleVec(int2 v) { return (double2)v * (1.0 / 65536.0); }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static int MUL_26D6(int a, int b) { return (int)(((long)a * (long)b) >> 6); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MUL_26D6(int a, int b) { return (int)(((double)a * (double)b) / 64); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int VEC_26D6_DOT(int2 p, int2 q) { return MUL_26D6(p.x, q.x) + MUL_26D6(p.y, q.y); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FT_ABS(int a) { return (a) < 0 ? -(a) : (a); }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static int FT_ABS(int a) { return math.abs(a); }

        public static int VECTOR_LENGTH_16D16(int2 v)
        {
            if (SDFCommon.USE_SQUARED_DISTANCES)
                return Fixed32.Mul(v.x, v.x) + Fixed32.Mul(v.y, v.y);
            else
            {
                var tmp = ToDoubleVec(v);
                var length = math.length(tmp);
                return Fixed32.FromDouble(length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static SignedDistanceFixed resolve_corner(SignedDistanceFixed sdf1, SignedDistanceFixed sdf2)
        {
            return FT_ABS(sdf1.cross) > FT_ABS(sdf2.cross) ? sdf1 : sdf2;
        }
        static int SquareRoot(int val)
        {
            ulong t, q, b, r;
            r = (ulong)val;
            b = 0x40000000L;
            q = 0;
            while (b > 0x40L)
            {
                t = q + b;
                if (r >= t)
                {
                    r -= t;
                    q = t + b;
                }
                r <<= 1;
                b >>= 1;
            }
            q >>= 8;
            return (int)q;
        }
        public static SignedDistanceFixed ResolveCorner(SignedDistanceFixed sdf1, SignedDistanceFixed sdf2)
        {
            return FT_ABS(sdf1.cross) > FT_ABS(sdf2.cross) ? sdf1 : sdf2;
        }

        static void MapFixedToSDF(int dist, int max_value, out byte alpha8)
        {
            int udist;


            /* normalize the distance values */
            dist = Fixed32.DivPrecise( dist, max_value );

            udist = dist < 0 ? -dist : dist;

            /* Reduce the distance values to 8 bits.                   */
            /*                                                         */
            /* Since +1/-1 in 16.16 takes the 16th bit, we right-shift */
            /* the number by 9 to make it fit into the 7-bit range.    */
            /*                                                         */
            /* One bit is reserved for the sign.                       */
            udist >>= 9;

            /* Since `char` can only store a maximum positive value    */
            /* of 127 we need to make sure it does not wrap around and */
            /* give a negative value.                                  */
            if ( dist > 0 && udist > 127 )
                udist = 127;
            if ( dist < 0 && udist > 128 )
                udist = 128;

                /* Output the data; negative values are from [0, 127] and positive    */
                /* from [128, 255].  One important thing is that negative values      */
                /* are inverted here, that means [0, 128] maps to [-128, 0] linearly. */
                /* More on that in `freetype.h` near the documentation of             */
                /* `FT_RENDER_MODE_SDF`.                                              */
                //alpha8 = dist < 0 ? 128 - (byte)udist : (byte)udist + 128;
                alpha8 = dist < 0 ? (byte)(128 - udist) : (byte)(udist + 128);
            }

        //int FT_Vector_Length(int2 vec)
        //{
        //    int shift;
        //    int2 v;


        //    v = vec;

        //    /* handle trivial cases */
        //    if (v.x == 0)
        //    {
        //        return (v.y >= 0) ? v.y : -v.y;
        //    }
        //    else if (v.y == 0)
        //    {
        //        return (v.x >= 0) ? v.x : -v.x;
        //    }

        //    /* general case */
        //    shift = ft_trig_prenorm(v);
        //    ft_trig_pseudo_polarize(&v);

        //    v.x = ft_trig_downscale(v.x);

        //    if (shift > 0)
        //        return (v.x + (1 << (shift - 1))) >> shift;

        //    return v.x << -shift;
        //}


        ///* documentation is in fttrigon.h */

        //FT_EXPORT_DEF(void )
        //  FT_Vector_Polarize(FT_Vector* vec,
        //                      FT_Fixed* length,
        //                      FT_Angle* angle)
        //{
        //    FT_Int shift;
        //    FT_Vector v;


        //    v = *vec;

        //    if (v.x == 0 && v.y == 0)
        //        return;

        //    shift = ft_trig_prenorm(&v);
        //    ft_trig_pseudo_polarize(&v);

        //    v.x = ft_trig_downscale(v.x);

        //    *length = (shift >= 0) ? (v.x >> shift) : (v.x << -shift);
        //    *angle = v.y;
        //}

        ///* undefined and never called for zero vector */
        //static int ft_trig_prenorm(int2 vec)
        //{
        //    int x, y;
        //    int shift;
        //    x = vec.x;
        //    y = vec.y;
        //    shift = FT_MSB((uint)(FT_ABS(x) | FT_ABS(y)));
        //    if (shift <= FT_TRIG_SAFE_MSB)
        //    {
        //        shift = FT_TRIG_SAFE_MSB - shift;
        //        vec.x = (int)((ulong)x << shift);
        //        vec.y = (int)((ulong)y << shift);
        //    }
        //    else
        //    {
        //        shift -= FT_TRIG_SAFE_MSB;
        //        vec.x = x >> shift;
        //        vec.y = y >> shift;
        //        shift = -shift;
        //    }
        //    return shift;
        //}

        //static int FT_MSB(uint z)
        //{
        //    int shift = 0;


        //    /* determine msb bit index in `shift' */
        //    if ((z & 0xFFFF0000UL) > 0)
        //    {
        //        z >>= 16;
        //        shift += 16;
        //    }
        //    if ((z & 0x0000FF00UL) > 0)
        //    {
        //        z >>= 8;
        //        shift += 8;
        //    }
        //    if ((z & 0x000000F0UL) > 0)
        //    {
        //        z >>= 4;
        //        shift += 4;
        //    }
        //    if ((z & 0x0000000CUL) > 0)
        //    {
        //        z >>= 2;
        //        shift += 2;
        //    }
        //    if ((z & 0x00000002UL) > 0)
        //    {
        //        /* z     >>= 1; */
        //        shift += 1;
        //    }

        //    return shift;
        //}
        //static void ft_trig_pseudo_polarize(int2 vec)
        //{
        //    int theta;
        //    int i;
        //    int x, y, xtemp, b;
        //    const FT_Angle* arctanptr;


        //    x = vec->x;
        //    y = vec->y;

        //    /* Get the vector into [-PI/4,PI/4] sector */
        //    if (y > x)
        //    {
        //        if (y > -x)
        //        {
        //            theta = FT_ANGLE_PI2;
        //            xtemp = y;
        //            y = -x;
        //            x = xtemp;
        //        }
        //        else
        //        {
        //            theta = y > 0 ? FT_ANGLE_PI : -FT_ANGLE_PI;
        //            x = -x;
        //            y = -y;
        //        }
        //    }
        //    else
        //    {
        //        if (y < -x)
        //        {
        //            theta = -FT_ANGLE_PI2;
        //            xtemp = -y;
        //            y = x;
        //            x = xtemp;
        //        }
        //        else
        //        {
        //            theta = 0;
        //        }
        //    }

        //    arctanptr = ft_trig_arctan_table;

        //    /* Pseudorotations, with right shifts */
        //    for (i = 1, b = 1; i < FT_TRIG_MAX_ITERS; b <<= 1, i++)
        //    {
        //        if (y > 0)
        //        {
        //            xtemp = x + ((y + b) >> i);
        //            y = y - ((x + b) >> i);
        //            x = xtemp;
        //            theta += *arctanptr++;
        //        }
        //        else
        //        {
        //            xtemp = x - ((y + b) >> i);
        //            y = y + ((x + b) >> i);
        //            x = xtemp;
        //            theta -= *arctanptr++;
        //        }
        //    }

        //    /* round theta to acknowledge its error that mostly comes */
        //    /* from accumulated rounding errors in the arctan table   */
        //    if (theta >= 0)
        //        theta = FT_PAD_ROUND(theta, 16);
        //    else
        //        theta = -FT_PAD_ROUND(-theta, 16);

        //    vec->x = x;
        //    vec->y = theta;
        //}
    }
}

