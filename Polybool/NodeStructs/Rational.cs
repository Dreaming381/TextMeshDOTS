using System;
using System.Diagnostics;

namespace TextMeshDOTS.Polybool
{
    /// <summary> Use this to store t of the equation a(t) = a0 + t * (a1 - a0) as a rational number.</summary>
    [DebuggerDisplay("{num} / {den}")]
    public readonly struct Rational : IEquatable<Rational>
    {
        public readonly long num; // numerator of t
        public readonly long den; // denominator of t (must be > 0)
        /// <summary> Use this to store the t fraction of the equation a(t) = a0 + t * (a1 - a0) as a rational number.</summary>
        public Rational(long num, long den)
        {
            if (den < 0)
            {
                num = -num;
                den = -den;
            }
            this.num = num;
            this.den = den;
        }

        public bool IsOne()
        {
            return num == den;
        }
        public bool IsZero()
        {
            return num == 0;
        }
        /// <summary> returns -1 if point1 is smaller then point2</summary>
        public static int Compare(Rational a, Rational b, ref Segment aSeg, ref Segment bSeg)
        {
            var compX = CompareX(a, b, ref aSeg, ref bSeg);
            if (compX == 0)
                return CompareY(a, b, ref aSeg, ref bSeg);
            return compX;
        }
        public static int CompareX(Rational a, Rational b, ref Segment aSeg, ref Segment bSeg)
        {
            var aSegY0 = aSeg.p0.x;
            var aSegY1 = aSeg.p1.x;
            var bSegY0 = bSeg.p0.x;
            var bSegY1 = bSeg.p1.x;
            return CompareCoord(aSegY0, aSegY1 - aSegY0, a.num, a.den,
                                bSegY0, bSegY1 - bSegY0, b.num, b.den);
        }
        public static int CompareY(Rational a, Rational b, ref Segment aSeg, ref Segment bSeg)
        {
            var aSegY0 = aSeg.p0.y;
            var aSegY1 = aSeg.p1.y;
            var bSegY0 = bSeg.p0.y;
            var bSegY1 = bSeg.p1.y;
            return CompareCoord(aSegY0, aSegY1 - aSegY0, a.num, a.den,
                                bSegY0, bSegY1 - bSegY0, b.num, b.den);
        }
        static int CompareCoord(
                long p0a, long dpa, long na, long da,
                long p0b, long dpb, long nb, long db)
        {
            // (p0 + t*dp) but expanded and cross-multiplied
            // (p0a*da + na*dpa) * db  vs  (p0b*db + nb*dpb) * da

            //checked
            //{
            //    long lhs = (p0a * da + na * dpa) * db;
            //    long rhs = (p0b * db + nb * dpb) * da;

            //    return lhs < rhs ? -1 : lhs > rhs ? 1 : 0;
            //}

            // typecast to double to avoid potential int overflow (e.g. in clipper polygons.txt test 114)
            // review if we can adopt here 128 bit int multiplication  (MultiplyUInt64)
            double lhs = ((double)p0a * da + (double)na * dpa) * db;
            double rhs = ((double)p0b * db + (double)nb * dpb) * da;

            return lhs < rhs ? -1 : lhs > rhs ? 1 : 0;
        }
        public static int Compare(Rational a, Rational b)
        {
            // a.Num / a.Den ? b.Num / b.Den
            return (a.num * b.den).CompareTo(b.num * a.den);
        }

        public bool InRangeStrict()
        {
            return num > 0 && num < den;
        }
        public static bool InRange(Rational t, Rational min, Rational max)
        {
            return Compare(t, min) >= 0 && Compare(t, max) <= 0;
        }
        public bool InRangeInclusive()
        {
            return num >= 0 && num <= den;
        }
        public static bool InRangeStrict(Rational t, Rational min, Rational max)
        {
            return Compare(t, min) > 0 && Compare(t, max) < 0;
        }

        public static Rational ProjectPointOntoSegmentLine(long2 p, long2 a0, long adx, long ady, long aLen2)
        {
            long num = (p.x - a0.x) * adx + (p.y - a0.y) * ady;
            return new Rational(num, aLen2);
        }
        public static Rational Min(Rational a, Rational b) => Compare(a, b) <= 0 ? a : b;

        public static Rational Max(Rational a, Rational b) => Compare(a, b) >= 0 ? a : b;

        

        public override bool Equals(object obj)
        {
            return obj is Rational other && Equals(other);
        }
        public bool Equals(Rational other)
        {
            return num == other.num && den == other.den;
        }

        public static bool operator ==(Rational e1, Rational e2)
        {
            return e1.num == e2.num && e1.den == e2.den;
        }
        public static bool operator !=(Rational e1, Rational e2)
        {
            return !(e1 == e2);
        }

        public override int GetHashCode()
        {
            //return HashCode.Combine(num, den);
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + num.GetHashCode();
            hashCode = hashCode * -1521134295 + den.GetHashCode();
            return hashCode;
        }
    }
}
