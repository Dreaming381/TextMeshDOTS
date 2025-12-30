using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TextMeshDOTS.Polybool
{
    /// <summary> Use this to store t of the equation a(t) = a0 + t * (a1 - a0) as a rational number.</summary>
    [DebuggerDisplay("{num} / {den}")]
    public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
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
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> check if rational is larger than 0 and smaller than 1 </summary>
        public bool InRangeStrict()
        {
            return num > 0 && num < den;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> Check if rational is between (including) 0 and 1 </summary>
        public bool InRangeInclusive()
        {
            return num >= 0 && num <= den;
        }

        /// <summary> check if rational t is larger than rational min and smaller than max </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InRangeStrict(Rational t, Rational min, Rational max)
        {
            return t.CompareTo(min) > 0 && t.CompareTo(max) < 0;
        }

        /// <summary> Check if rational is between (including) rational min and max </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InRangeInclusive(Rational t, Rational min, Rational max)
        {
            return t.CompareTo(min) >= 0 && t.CompareTo(max) <= 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rational ProjectPointOntoSegmentLine(long2 p, long2 a0, long adx, long ady, long aLen2)
        {
            long num = (p.x - a0.x) * adx + (p.y - a0.y) * ady;
            return new Rational(num, aLen2);
        }
        public static Rational Min(Rational a, Rational b) => a.CompareTo(b) <= 0 ? a : b;

        public static Rational Max(Rational a, Rational b) => a.CompareTo(b) >= 0 ? a : b;        

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
        public int CompareTo(Rational other)
        {
            return (num * other.den).CompareTo(other.num * den);
        }
    }
}
