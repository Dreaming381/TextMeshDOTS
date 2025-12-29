using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TextMeshDOTS.Polybool
{
    [DebuggerDisplay("{x} {y}")]
    public struct long2 : IEquatable<long2>, IComparable<long2>
    {
        public long x;
        public long y;
        public long2(long2 pt)
        {
            x = pt.x;
            y = pt.y;
        }
        public long2(long x, long y)
        {
            this.x = x;
            this.y = y;
        }
        public long2(double x, double y)
        {
            this.x = (long)Math.Round(x, MidpointRounding.AwayFromZero);
            this.y = (long)Math.Round(y, MidpointRounding.AwayFromZero);
        }
        public long2(double x, double y, double scale)
        {
            this.x = (long)Math.Round(x * scale, MidpointRounding.AwayFromZero);
            this.y = (long)Math.Round(y * scale, MidpointRounding.AwayFromZero);
        }

        public static long2 operator +(long2 lhs, long2 rhs) { return new long2(lhs.x + rhs.x, lhs.y + rhs.y); }
        public static long2 operator -(long2 lhs, long2 rhs) { return new long2(lhs.x - rhs.x, lhs.y - rhs.y); }
        public static long2 operator *(double lhs, long2 rhs) { return new long2(lhs * rhs.x, lhs * rhs.y); }
        public static long2 operator *(long2 lhs, double rhs) { return new long2(lhs.x * rhs, lhs.y * rhs); }
        public override bool Equals(object obj)
        {
            return obj is long2 other && Equals(other);
        }
        public bool Equals(long2 other)
        {
            return x == other.x && y == other.y;
        }
        public static bool operator ==(long2 lhs, long2 rhs) { return lhs.x == rhs.x && lhs.y == rhs.y; }
        public static bool operator !=(long2 lhs, long2 rhs) { return lhs.x != rhs.x || lhs.y != rhs.y; }

        public readonly override int GetHashCode()
        {
            //return HashCode.Combine(x, y);
            unchecked // Overflow is fine
            {
                int hashCode = 17; // Start with a prime number
                hashCode = hashCode * 23 + x.GetHashCode();
                hashCode = hashCode * 23 + y.GetHashCode();
                return hashCode;
            }
        }
        public override string ToString()
        {
            return $"{x} {y}";
        }

        /// <summary> returns -1 if point1 is smaller then point2</summary>
        public int CompareTo(long2 other)
        {
            if (x == other.x)
                return (y == other.y) ? 0 : y < other.y ? -1 : 1;
            return x < other.x ? -1 : 1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 Lerp_blend(long2 a, long2 b, double t)
        {
            var res = a * (1.0 - t) + (b * t);
            return res;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 Lerp(long2 a, long2 b, double t)
        {
            long2 res = a + (b - a) * t;
            return res;
        }
    }
}