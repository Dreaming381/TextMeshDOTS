using FixPointCS;
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

        public static bool SDFGenerate(NativeList<SDFEdge> shape, int spread, ref NativeArray<byte> buffer, int width, int rows)
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
                    int2 grid_point; //FT_26D6_Vec
                    SignedDistanceFixed min_dist = max_sdf;

                    int index;
                    int value;
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
                    //min_dist.distance /= 64; /* convert from 16.16 to 22.10 */
                    //value = min_dist.distance & 0x0000FFFF; /* truncate to 6.10 */

                    min_dist.distance /= 65536;
                    value = min_dist.distance & 0x000000FF;
                    value *= min_dist.sign;
                    if (flip_y)
                        index = y * width + x;
                    else
                        index = (rows - y - 1) * width + x;
                    //buffer[index] = (byte)((value / 1024 + spread) * 24);
                    //minDistances[index] = ((value / 1024 + spread) * 24);
                    buffer[index] = (byte)((value + spread) * 24);
                    //minDistances[index] = ((value + spread) * 24);
                }
            }
            //SDFCommon.WriteMinDistancesToFile("DistancesFixed.txt", minDistances);
            return true;
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
                    Debug.Log($"LINE Not implemented");
                    //success = GetMinDistanceCubic(edge, point, out signedDistance);
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
