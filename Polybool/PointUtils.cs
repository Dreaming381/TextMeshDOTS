using System;
using System.Runtime.CompilerServices;
using TextMeshDOTS.HarfBuzz;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{
    public static class PointUtils
    {		
        /// <summary>Finds the magnitude of the cross product of two vectors (if we pretend they're in three dimensions) </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The magnitude of the cross product</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CrossProduct(double ax, double ay, double bx, double by)
        {
            return (ax * by) - (ay * bx);
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
        /// <summary>
        /// Returns a positive value if the points a, b, and p occur in counterclockwise order (CCW, p lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(CW, p lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Orient2DAccurate(long2 pt1, long2 pt2, long2 pt3)
        {
            long a = pt2.x - pt1.x;
            long b = pt3.y - pt2.y;
            long c = pt2.y - pt1.y;
            long d = pt3.x - pt2.x;
            UInt128Struct ab = MultiplyUInt64((ulong) math.abs(a), (ulong) math.abs(b));
            UInt128Struct cd = MultiplyUInt64((ulong) math.abs(c), (ulong) math.abs(d));
            int signAB = TriSign(a) * TriSign(b);
            int signCD = TriSign(c) * TriSign(d);

            if (signAB == signCD)
            {
                int result;
                if (ab.hi64 == cd.hi64)
                {
                    if (ab.lo64 == cd.lo64) return 0;
                    result = (ab.lo64 > cd.lo64) ? 1 : -1;
                }
                else result = (ab.hi64 > cd.hi64) ? 1 : -1;
                return (signAB > 0) ? result : -result;
            }
            return (signAB > signCD) ? 1 : -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCollinear(long2 pt1, long2 sharedPt, long2 pt2)
        {
            long a = sharedPt.x - pt1.x;
            long b = pt2.y - sharedPt.y;
            long c = sharedPt.y - pt1.y;
            long d = pt2.x - sharedPt.x;
            // When checking for collinearity with very large coordinate values
            // then ProductsAreEqual is more accurate than using CrossProduct.
            return ProductsAreEqual(a, b, c, d);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TriSign(long x) // returns 0, 1 or -1
        {
            return (x < 0) ? -1 : (x > 0) ? 1 : 0;
        }
        public struct UInt128Struct
        {
            public ulong lo64;
            public ulong hi64;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt128Struct MultiplyUInt64(ulong a, ulong b) // #834,#835
        {
            ulong x1 = (a & 0xFFFFFFFF) * (b & 0xFFFFFFFF);
            ulong x2 = (a >> 32) * (b & 0xFFFFFFFF) + (x1 >> 32);
            ulong x3 = (a & 0xFFFFFFFF) * (b >> 32) + (x2 & 0xFFFFFFFF);
            UInt128Struct result;
            result.lo64 = (x3 & 0xFFFFFFFF) << 32 | (x1 & 0xFFFFFFFF);
            result.hi64 = (a >> 32) * (b >> 32) + (x2 >> 32) + (x3 >> 32);
            return result;
        }
        // returns true if (and only if) a * b == c * d
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ProductsAreEqual(long a, long b, long c, long d)
        {
            // nb: unsigned values will be needed for CalcOverflowCarry()
            ulong absA = (ulong) math.abs(a);
            ulong absB = (ulong) math.abs(b);
            ulong absC = (ulong) math.abs(c);
            ulong absD = (ulong) math.abs(d);

            UInt128Struct mul_ab = MultiplyUInt64(absA, absB);
            UInt128Struct mul_cd = MultiplyUInt64(absC, absD);

            // nb: it's important to differentiate 0 values here from other values
            int sign_ab = TriSign(a) * TriSign(b);
            int sign_cd = TriSign(c) * TriSign(d);

            return mul_ab.lo64 == mul_cd.lo64 && mul_ab.hi64 == mul_cd.hi64 && sign_ab == sign_cd;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWindingTowardsBottom(long2 a, long2 b)
        {
            return Math.Sign(b.x - a.x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWindingTowardsRight(long2 a, long2 b)
        {
            return Math.Sign(b.y - a.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Snap01(double v)
        {
            if (math.abs(v) < BezierMath.epsilon1_abs) return 0;
            if (math.abs(1 - v) < BezierMath.epsilon1_abs) return 1;
            return v;
        }
    }
}