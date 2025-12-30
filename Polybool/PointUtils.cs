using System;
using System.Runtime.CompilerServices;


namespace TextMeshDOTS.Polybool
{
    public static class PointUtils
    {
        internal const float epsilon1Float = 1e-6f;   // at 1, next representable float step (ULP) is +- 2^(0 - 23) = 1.19e-7
        internal const double epsilon1_abs = 1e-12;       // at 1, next representable double step (ULP) is +- 2^(0 - 52) = 2.22045e-16
        internal const double epsilon1_rel = 1e-16;       // at 1, next representable double step (ULP) is +- 2^(0 - 52) = 2.22045e-16

        internal const float epsilon100Float_abs = 1e-5f; // at 100, next representable float step (ULP) is +- 2^(6 - 23) = 7.62939e-06		
        internal const double epsilon100_rel = 1e-10;     // at 100, next representable double step (ULP) is +- 2^(6 - 52) = 1.42109E-14


        /// <summary>Finds the magnitude of the cross product of two vectors (if we pretend they're in three dimensions) </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The magnitude of the cross product</returns>
        public static long CrossProduct(long ax, long ay, long bx, long by)
        {
            // typecast to double to avoid potential int overflow
            return (long)((double)ax * by - (double)ay * bx); //review if we can adop here 128 bit int multiplication  (MultiplyUInt64)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long DotProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            return ((pt2.x - pt1.x) * (pt3.x - pt2.x) +
                    (pt2.y - pt1.y) * (pt3.y - pt2.y));
        }
        /// <summary>
        /// Returns a positive value if the points a, b, and p occur in counterclockwise order (CCW, p lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(CW, p lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// Result also happens to be twice the signed area of the triangle
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static double Orient2DFast(long2 a, long2 b, long2 p)
		{
			return (a.x - p.x) * (b.y - p.y) - (a.y - p.y) * (b.x - p.x);
		}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Orient2DParamPoint(long2 a, long2 b, Rational p, ref Segment pSeg)
        {
            // orient(a, b, p(t))
            // = orient(a, b, p0 + t*(p1 - p0))
            // = orient(a, b, p0) + t * orient(a, b, (p1 - p0))

            long dx = pSeg.p1.x - pSeg.p0.x;
            long dy = pSeg.p1.y - pSeg.p0.y;

            // orient(a, b, p0)
            long baseTerm =
                (b.x - a.x) * (pSeg.p0.y - a.y) -
                (b.y - a.y) * (pSeg.p0.x - a.x);

            // orient(a, b, direction)
            long dirTerm =
                (b.x - a.x) * dy -
                (b.y - a.y) * dx;

            // Combine as rational value:
            // result = baseTerm * d + dirTerm * n
            long value = baseTerm * p.den + dirTerm * p.num;

            return Math.Sign(value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCollinear(long2 pt1, long2 sharedPt, long2 pt2)
        {
            return Math.Abs(Orient2DFast(pt1, pt2, sharedPt)) < epsilon1_abs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetWindingTowardsBottom(long2 a, long2 b)
        {
            return Math.Sign(b.x - a.x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetWindingTowardsRight(long2 a, long2 b)
        {
            return Math.Sign(b.y - a.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool PtsReallyClose(long2 pt1, long2 pt2)
        {
            return (Math.Abs(pt1.x - pt2.x) < 2) && (Math.Abs(pt1.y - pt2.y) < 2);
        }
       
    }
}