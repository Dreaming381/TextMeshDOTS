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
            this.x = (long) Math.Round(x, MidpointRounding.AwayFromZero);
            this.y = (long) Math.Round(y, MidpointRounding.AwayFromZero);
        }
        public long2(double x, double y, double scale)
        {
            this.x = (long) Math.Round(x * scale, MidpointRounding.AwayFromZero);
            this.y = (long) Math.Round(y * scale, MidpointRounding.AwayFromZero);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator +(long2 lhs, long2 rhs) { return new long2(lhs.x + rhs.x, lhs.y + rhs.y); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator -(long2 lhs, long2 rhs) { return new long2(lhs.x - rhs.x, lhs.y - rhs.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator *(double lhs, long2 rhs) { return new long2(lhs * rhs.x, lhs * rhs.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator *(long2 lhs, double rhs) { return new long2(lhs.x * rhs, lhs.y * rhs); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj != null && obj is long2 p)
                return this == p;
            else
                return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(long2 other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(long2 lhs, long2 rhs) { return lhs.x == rhs.x && lhs.y == rhs.y; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(long2 lhs, long2 rhs) { return lhs.x != rhs.x || lhs.y != rhs.y; }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }
        public override string ToString()
        {
            return $"{x} {y}";
        }

        /// <summary> returns -1 if point1 is smaller then point2</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PointsCompare(long2 point1, long2 point2)
        {
            if (point1.x == point2.x)
                return (point1.y == point2.y) ? 0 : point1.y < point2.y ? -1 : 1;
            return point1.x < point2.x ? -1 : 1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var res = a + (b - a) * t;
            return res;
        }
    }
}