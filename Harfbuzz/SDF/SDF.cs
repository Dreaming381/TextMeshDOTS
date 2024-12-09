using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HarfBuzz.SDF
{
    public static class SDF
    {
        public static SignedDistance max_sdf => new SignedDistance { distance = int.MaxValue, sign = 0, cross = 0 };

        public const float CORNER_CHECK_EPSILON = 32 / (1 << 16); //The epsilon distance  used for corner

        public static bool SDFGenerateSubDivision(NativeList<SDFEdge> edge_list, int spread, NativeArray<byte> buffer, int width, int rows)
        {
            var success = true;
            var new_edges = new NativeList<SDFEdge>(edge_list.Length, Allocator.Temp);
            success = SplitSDFShape(edge_list, new_edges);
            success = SDFGenerateBoundingBox(new_edges, spread, buffer, width, rows);
            return success;
        }
        public static bool SDFGenerate(NativeList<SDFEdge> shape, float spread, ref NativeArray<byte> buffer, int width, int rows)
        {
            bool flip_y = true;
            SDFOrientation orientation = SDFOrientation.TRUETYPE;
            bool flip_sign = true;
            float sp_sq;   /* `spread` [* `spread`] as a 16.16 fixed value */

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;


            if (SDFCommon.USE_SQUARED_DISTANCES)
                sp_sq = spread * spread;
            else
                sp_sq = spread;

            if (width == 0 || rows == 0)
            {
                Debug.Log($"sdf_generate:  Cannot render glyph with width/height == 0 (width: {width}, height: {rows})");
                return false;
            }
            //var minDistances = new NativeArray<float>(buffer.Length, Allocator.Temp);
            /* loop over all rows */
            for (int y = 0; y < rows; y++)
            {
                /* loop over all pixels of a row */
                for (int x = 0; x < width; x++)
                {
                    /* `grid_point` is the current pixel position; */
                    /* our task is to find the shortest distance   */
                    /* from this point to the entire shape.        */
                    float2 grid_point;
                    SignedDistance min_dist = max_sdf;
                    int index;
                    float value;

                    grid_point.x = x;
                    grid_point.y = y;

                    /* This `grid_point' is at the corner, but we */
                    /* use the center of the pixel.               */
                    grid_point.x += 1f / 2f;
                    grid_point.y += 1f / 2f;

                    //contour_list = shape->contours;
                    /* iterate over all contours manually */
                    //for (int i = 0, length=shape.Length; i < length; i++)
                    //{
                        SignedDistance current_dist = max_sdf;
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
                        min_dist.distance = math.sqrt(min_dist.distance);
                    
                    if (orientation == SDFOrientation.FILL_LEFT)
                        min_dist.sign = -min_dist.sign;
                    if (flip_sign)
                        min_dist.sign = -min_dist.sign;
                    //minDistanceRaw /= 64; /* convert from 16.16 to 22.10 */
                    //value = minDistanceRaw & 0x0000FFFF; /* truncate to 6.10 */
                    value = min_dist.distance;
                    value *= min_dist.sign;
                    if (flip_y)
                        index = y * width + x;
                    else
                        index = (rows - y - 1) * width + x;
                    buffer[index] = (byte)((value + spread) * 8);
                    //minDistances[index] = ((value + spread) * 24);
                }
            }
            //SDFCommon.WriteMinDistancesToFile("Distances.txt", minDistances);
            return true;
        }

        static bool SplitSDFShape(NativeList<SDFEdge> edge_list, NativeList<SDFEdge> new_edges)
        {
            bool success = true;
            /* for each edge */
            for (int i = 0, length = edge_list.Length; i < length; i++)
            {
                var edge = edge_list[i];

                switch (edge.edge_type)
                {
                    case SDFEdgeType.LINE:
                    case SDFEdgeType.CONIC:
                    case SDFEdgeType.CUBIC:
                        edge.edge_type = SDFEdgeType.LINE;
                        new_edges.Add(edge);
                        break;

                    default:
                        break;
                        //error = FT_THROW(Invalid_Argument);
                }
            }
            return success;
        }
        static bool SDFGenerateBoundingBox(NativeList<SDFEdge> edge_list, int spread, NativeArray<byte> buffer, int width, int rows)
        {
            bool flip_y = true;
            SDFOrientation orientation = SDFOrientation.TRUETYPE;
            bool flip_sign = true;
            int overloadSign = 0;
            float sp_sq;   /* `spread` [* `spread`] as a 16.16 fixed value */
            var dists = new NativeArray<SignedDistance>(width * rows, Allocator.Temp);

            if (spread < SDFCommon.MIN_SPREAD || spread > SDFCommon.MAX_SPREAD)
                return false;

            if (SDFCommon.USE_SQUARED_DISTANCES)
                sp_sq = spread * spread;
            else
                sp_sq = spread;

            int maxIndex = int.MinValue;
            int minIndex = int.MaxValue;
            /* loop over all edges */
            for (int i = 0, length = edge_list.Length; i < length; i++)
            {
                var edge = edge_list[i];
                Rect cbox;
                int x, y;


                /* get the control box and increase it by `spread' */
                cbox = GetControlBox(edge);

                cbox.xMin = cbox.xMin - spread;
                cbox.xMax = cbox.xMax + spread;
                cbox.yMin = cbox.yMin - spread;
                cbox.yMax = cbox.yMax + spread;

                //Debug.Log($"x {cbox.x} y {cbox.y} width {cbox.width} height {cbox.height}");

                /* now loop over the pixels in the control box. */
                for (y = (int)cbox.yMin; y < cbox.yMax; y++)
                {
                    for (x = (int)cbox.xMin; x < cbox.xMax; x++)
                    {
                        float2 grid_point;
                        SignedDistance dist = max_sdf;
                        int index = 0;
                        float diff = 0;

                        if (x < 0 || x >= width)
                            continue;
                        if (y < 0 || y >= rows)
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
                            index = y * width + x;
                        else
                            index = (rows - y - 1) * width + x;

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

            /* final pass */
            //var minDistances = new NativeArray<float>(buffer.Length, Allocator.Temp);
            int outsideSign = -1;
            for (var j = 0; j < rows; j++)
            {
                /* We assume the starting pixel of each row is outside. */
                int current_sign = outsideSign;
                int index;


                if (overloadSign != 0)
                    current_sign = overloadSign < 0 ? -1 : 1;

                for (int i = 0; i < width; i++)
                {
                    index = j * width + i;

                    /* if the pixel is not set                     */
                    /* its shortest distance is more than `spread` */
                    var dist = dists[index];
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
                    dists[index] = dist;

                    /* concatenate to appropriate format */
                    //buffer[index] = map_fixed_to_sdf(dist.distance, spread);
                    buffer[index] = (byte)((dist.distance + spread) * 24);
                    //minDistances[index] = ((dist.distance + spread) * 24);
                    //buffer[index] = (byte)dist.distance;
                    //minDistances[index] = dist.distance;
                }
            }
            //SDFCommon.WriteMinDistancesToFile("DistancesSubDivide.txt", minDistances);
            return true;
        }
        static Rect GetControlBox(SDFEdge edge)
        {
            Rect cbox = new Rect(0, 0, 0, 0);
            bool is_set = false;


            switch (edge.edge_type)
            {
                case SDFEdgeType.CUBIC:
                    cbox.xMin = edge.control2.x;
                    cbox.xMax = edge.control2.x;
                    cbox.yMin = edge.control2.y;
                    cbox.yMax = edge.control2.y;

                    is_set = true;
                    goto case SDFEdgeType.CONIC;

                case SDFEdgeType.CONIC:
                    if (is_set)
                    {
                        cbox.xMin = edge.control1.x < cbox.xMin
                                    ? edge.control1.x
                                    : cbox.xMin;
                        cbox.xMax = edge.control1.x > cbox.xMax
                                    ? edge.control1.x
                                    : cbox.xMax;

                        cbox.yMin = edge.control1.y < cbox.yMin
                                    ? edge.control1.y
                                    : cbox.yMin;
                        cbox.yMax = edge.control1.y > cbox.yMax
                                    ? edge.control1.y
                                    : cbox.yMax;
                    }
                    else
                    {
                        cbox.xMin = edge.control1.x;
                        cbox.xMax = edge.control1.x;
                        cbox.yMin = edge.control1.y;
                        cbox.yMax = edge.control1.y;

                        is_set = true;
                    }
                    goto case SDFEdgeType.LINE;

                case SDFEdgeType.LINE:
                    if (is_set)
                    {
                        cbox.xMin = edge.start_pos.x < cbox.xMin
                                    ? edge.start_pos.x
                                    : cbox.xMin;
                        cbox.xMax = edge.start_pos.x > cbox.xMax
                                    ? edge.start_pos.x
                                    : cbox.xMax;

                        cbox.yMin = edge.start_pos.y < cbox.yMin
                                    ? edge.start_pos.y
                                    : cbox.yMin;
                        cbox.yMax = edge.start_pos.y > cbox.yMax
                                    ? edge.start_pos.y
                                    : cbox.yMax;
                    }
                    else
                    {
                        cbox.xMin = edge.start_pos.x;
                        cbox.xMax = edge.start_pos.x;
                        cbox.yMin = edge.start_pos.y;
                        cbox.yMax = edge.start_pos.y;
                    }

                    cbox.xMin = edge.end_pos.x < cbox.xMin
                                ? edge.end_pos.x
                                : cbox.xMin;
                    cbox.xMax = edge.end_pos.x > cbox.xMax
                                ? edge.end_pos.x
                                : cbox.xMax;

                    cbox.yMin = edge.end_pos.y < cbox.yMin
                                ? edge.end_pos.y
                                : cbox.yMin;
                    cbox.yMax = edge.end_pos.y > cbox.yMax
                                ? edge.end_pos.y
                                : cbox.yMax;

                    break;

                default:
                    break;
            }

            return cbox;
        }

        static bool SDFContourGetMinDistance(NativeList<SDFEdge> edge_list, float2 point, ref SignedDistance signedDistance)
        {
            SignedDistance min_dist = max_sdf;

            /* iterate over all the edges manually */
            for (int i = 0, length = edge_list.Length; i < length; i++)
            {
                var edge = edge_list[i];
                SignedDistance current_dist = max_sdf;
                float diff;
                SDFEdgeGetMinDistance(edge, point, ref current_dist);

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
            }
            signedDistance = min_dist;
            return true;
        }
        public static bool SDFEdgeGetMinDistance(SDFEdge edge, float2 point, ref SignedDistance signedDistance)
        {
            bool success = false;
            switch (edge.edge_type)
            {
                case SDFEdgeType.LINE:
                    success = GetMinDistanceLine(edge, point, ref signedDistance);
                    break;
                case SDFEdgeType.CONIC:
                    Debug.Log($"CONIC Not implemented");
                    //success = GetMinDistanceCubic(edge, point, ref signedDistance);
                    break;
                case SDFEdgeType.CUBIC:
                    success = GetMinDistanceCubic(edge, point, ref signedDistance);
                    break;
                default:
                    break;
            }
            return success;
        }
        static bool GetMinDistanceLine(SDFEdge line, float2 point, ref SignedDistance signedDistance)
        {
            float2 a;                   /* start position */
            float2 b;                   /* end position   */
            float2 p;                   /* current point  */

            float2 line_segment;      /* `b` - `a` */
            float2 p_sub_a;           /* `p` - `a` */

            float sq_line_length;       /* squared length of `line_segment` */
            float factor;               /* factor of the nearest point      */
            float cross;                /* used to determine sign           */

            float2 nearest_point;    /* `point_on_line`       */
            float2 nearest_vector;   /* `p` - `nearest_point` */


            if (line.edge_type != SDFEdgeType.LINE)
            {
                Debug.Log($"Edge is not a line: {line.edge_type}");
                return false;
            }

            a = line.start_pos;
            b = line.end_pos;
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
            if (SDFCommon.USE_SQUARED_DISTANCES)
                signedDistance.distance = math.lengthsq(nearest_vector);
            else
                signedDistance.distance = math.length(nearest_vector);

            /* Instead of finding `cross` for checking corner we */
            /* directly set it here.  This is more efficient     */
            /* because if the distance is perpendicular we can   */
            /* directly set it to 1.                             */
            //if (factor != 0 && factor != 1)
            if (!Equals(factor, 0) && !Equals(factor - 1, 0))
                signedDistance.cross = 1;
            else
            {
                /* [OPTIMIZATION]: Pre-compute this direction. */
                /* If not perpendicular then compute `cross`.  */
                math.normalize(line_segment);
                math.normalize(nearest_vector);

                signedDistance.cross = (line_segment.x * nearest_vector.y) -
                                       (line_segment.y * nearest_vector.x);
            }
            return true;
        }
        static bool GetMinDistanceCubic(SDFEdge cubic, float2 point, ref SignedDistance signedDistance)
        {
            float2 aA, bB, cC, dD; /* A, B, C in the above comment          */
            float2 nearest_point = default;  /* point on curve nearest to `point`     */
            float2 direction;      /* direction of curve at `nearest_point` */
            float2 p0, p1, p2, p3;  /* control points of a cubic curve       */
            float2 p;               /* `point` to which shortest distance    */
            float min_factor = 0;            /* factor at shortest distance */
            float min_factor_sq = 0;            /* factor at shortest distance */
            float cross;                        /* to determine the sign       */
            float min = int.MaxValue;   /* shortest distance           */

            if (cubic.edge_type != SDFEdgeType.CUBIC)
            {
                Debug.Log($"Edge is not cubic: {cubic.edge_type}");
                return false;
            }

            p0 = cubic.start_pos;
            p1 = cubic.control1;
            p2 = cubic.control2;
            p3 = cubic.end_pos;
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

            for (int iterations = 0; iterations <= SDFCommon.MAX_NEWTON_DIVISIONS; iterations++)
            {
                float factor = (float)iterations / SDFCommon.MAX_NEWTON_DIVISIONS;
                float factor2;          // factor^2
                float factor3;          // factor^3
                float length;

                float2 curve_point;     // point on the curve
                float2 dist_vector;     // `curve_point' - `p'

                float2 d1;              // first  derivative
                float2 d2;              // second derivative

                float temp1;
                float temp2;

                for (int steps = 0; steps < SDFCommon.MAX_NEWTON_STEPS; steps++)
                {
                    factor2 = factor * factor;
                    factor3 = factor2 * factor;

                    /* B(t) = t^3 * A + t^2 * B + t * C + D */
                    curve_point.x = aA.x * factor3 +
                                    bB.x * factor2 +
                                    cC.x * factor + dD.x;
                    curve_point.y = aA.y * factor3 +
                                    bB.y * factor2 +
                                    cC.y * factor + dD.y;

                    /* P(t) in the comment */
                    dist_vector.x = curve_point.x - p.x;
                    dist_vector.y = curve_point.y - p.y;

                    if(SDFCommon.USE_SQUARED_DISTANCES)
                        length=math.lengthsq(dist_vector);
                    else
                        length = math.length(dist_vector);

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
                    d1.x = aA.x * 3 * factor2 +
                           bB.x * 2 * factor + cC.x;
                    d1.y = aA.y * 3 * factor2 +
                           bB.y * 2 * factor + cC.y;

                    /* B''(t) = 6t * A + 2B */
                    d2.x = aA.x * 6 * factor + 2 * bB.x;
                    d2.y = aA.y * 6 * factor + 2 * bB.y;

                    //dist_vector.x /= 1024;
                    //dist_vector.y /= 1024;

                    /* temp1 = P(t) . B'(t) */
                    temp1 = math.dot(dist_vector, d1);

                    /* temp2 = B'(t) . B'(t) + P(t) . B''(t) */
                    temp2 = math.dot(d1, d1) +
                            math.dot(dist_vector, d2);

                    factor -= temp1 / temp2;

                    if (factor < 0 || factor > 1)
                        break;
                }
            }

            /* B'(t) = 3t^2 * A + 2t * B + C */
            direction.x = aA.x * 3 * min_factor_sq +
                          bB.x * 2 * min_factor + cC.x;

            direction.y = aA.y * 3 * min_factor_sq +
                          bB.y * 2 * min_factor + cC.y;

            /* determine the sign */
            cross = (nearest_point.x - p.x) * direction.y - (nearest_point.y - p.y) * direction.x;
            
            //* assign the values */
            signedDistance.distance = min;
            signedDistance.sign = cross < 0 ? (sbyte)1 : (sbyte)-1;
            if (!Equals(min_factor, 0) && !Equals(min_factor - 1, 0))
                signedDistance.cross = 1;   /* the two are perpendicular */
            else
            {
                /* convert to nearest vector */
                nearest_point.x -= p.x;
                nearest_point.y -= p.y;

                /* compute `cross` if not perpendicular */
                direction = math.normalize(direction);
                nearest_point = math.normalize(nearest_point);
                signedDistance.cross = direction.x * nearest_point.y - 
                                       direction.y * nearest_point.x;
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

    }
}
