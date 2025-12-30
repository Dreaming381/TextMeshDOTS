using System;
using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;

namespace TextMeshDOTS.Polybool
{
    public static class PointUtils128
    {
        /// <summary>
        /// Returns a positive value if the points a, b, and p occur in counterclockwise order (CCW, p lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(CW, p lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Orient2DAccurate(long2 pt1, long2 pt2, long2 pt3)
        {
            long a = pt2.x - pt1.x;
            long b = pt3.y - pt2.y;
            long c = pt2.y - pt1.y;
            long d = pt3.x - pt2.x;
            UInt128Struct ab = MultiplyUInt64((ulong) Math.Abs(a), (ulong) Math.Abs(b));
            UInt128Struct cd = MultiplyUInt64((ulong) Math.Abs(c), (ulong) Math.Abs(d));
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
        internal struct UInt128Struct
        {
            public ulong lo64;
            public ulong hi64;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static UInt128Struct MultiplyUInt64(ulong a, ulong b) // #834,#835
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
            ulong absA = (ulong) Math.Abs(a);
            ulong absB = (ulong) Math.Abs(b);
            ulong absC = (ulong) Math.Abs(c);
            ulong absD = (ulong) Math.Abs(d);

            UInt128Struct mul_ab = MultiplyUInt64(absA, absB);
            UInt128Struct mul_cd = MultiplyUInt64(absC, absD);

            // nb: it's important to differentiate 0 values here from other values
            int sign_ab = TriSign(a) * TriSign(b);
            int sign_cd = TriSign(c) * TriSign(d);

            return mul_ab.lo64 == mul_cd.lo64 && mul_ab.hi64 == mul_cd.hi64 && sign_ab == sign_cd;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128Like CrossProduct128(
            long ax, long ay,
            long bx, long by)
        {
            var p1 = Mul128(ax, by);
            var p2 = Mul128(ay, bx);
            return Sub128(p1, p2);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Int128Like Sub128(in Int128Like a, in Int128Like b)
        {
            ulong lo = a.lo64 - b.lo64;
            long borrow = (long) ((lo >> 63) & 1); // works because underflow sets MSB
            long hi = a.hi64 - b.hi64 - borrow;
            return new Int128Like(hi, lo);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128Like Add128(in Int128Like a, in Int128Like b)
        {
            ulong lo = a.lo64 + b.lo64;

            // carry = 1 if overflow occurred
            long carry = (long) ((lo >> 63) & ((a.lo64 >> 63) | (b.lo64 >> 63)));

            long hi = a.hi64 + b.hi64 + carry;

            return new Int128Like(hi, lo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128Like Mul128(long a, long b)
        {
            long lo;
            long hi = BigMul(a, b, out lo);
            return new Int128Like(hi, unchecked((ulong) lo));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128Like Mul128x64(in Int128Like a, long b)
        {
            long loLo;
            long loHi = BigMul(unchecked((long) a.lo64), b, out loLo);

            long hiLo;
            BigMul(a.hi64, b, out hiLo);

            ulong lo = unchecked((ulong) loLo);

            long hi = loHi + hiLo;

            // branchless carry detect
            hi += (long) (((ulong) hi < (ulong) loHi) ? 1UL : 0UL);

            return new Int128Like(hi, lo);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong BigMul(ulong a, ulong b, out ulong low)
        {
            // cannot access hardware intrinsics in .NetStandard 2.1
            // what a shame because mulx_u64 is support on pretty much all platfroms
            if (X86.Bmi2.IsBmi2Supported)
            {
                ulong tmp;
                ulong high = X86.Bmi2.mulx_u64(a, b, out tmp);
                low = tmp;
                return high;
            }

            return SoftwareFallback(a, b, out low);

            static ulong SoftwareFallback(ulong a, ulong b, out ulong low)
            {
                // It's adoption of algorithm for multiplication
                // of 32-bit unsigned integers described
                // in Hacker's Delight by Henry S. Warren, Jr. (ISBN 0-201-91465-4), Chapter 8
                // Basically, it's an optimized version of FOIL method applied to
                // low and high dwords of each operand
                const ulong lowBitsMask = 0xFFFFFFFFU;

                // Split the inputs into high/low sections.
                ulong al = a & lowBitsMask;
                ulong ah = a >> 32;
                ulong bl = b & lowBitsMask;
                ulong bh = b >> 32;

                ulong mull = al * bl;
                ulong t = ah * bl + (mull >> 32);
                ulong tl = t & lowBitsMask;

                tl += al * bh;
                low = tl << 32 | mull & lowBitsMask;

                return ah * bh + (t >> 32) + (tl >> 32);
            }
        }
        static long BigMul(long a, long b, out long low)
        {
            ulong high = BigMul((ulong) a, (ulong) b, out ulong ulow);
            low = (long) ulow;
            return (long) high - ((a >> 63) & b) - ((b >> 63) & a);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPositive128(in Int128Like v)
        {
            // returns -1, 0, or +1
            return v.hi64 > 0 || (v.hi64 == 0 && v.lo64 != 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign128(in Int128Like v)
        {
            // hiSign: -1 if Hi < 0, 0 if Hi >= 0
            int hiSign = (int) (v.hi64 >> 63);

            // hiNonZero: 1 if Hi != 0, 0 if Hi == 0
            int hiNonZero = (int) (((ulong) (v.hi64 | -v.hi64)) >> 63);

            // loNonZero: 1 if Lo != 0, 0 if Lo == 0
            int loNonZero = (int) ((v.lo64 | (ulong) -(long) v.lo64) >> 63);

            // If Hi != 0 → return sign of Hi
            // Else → return +1 if Lo != 0, else 0
            return hiSign | (loNonZero & ~hiNonZero);
        }
    }
}