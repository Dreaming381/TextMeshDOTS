/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Date      :  17 July 2023                                                    *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2023                                         *
* Purpose   :  Core structures and functions for the Clipper Library           *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/

using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.Clipper2AoS
{
    public struct Rect64
    {
        public long left;
        public long top;
        public long right;
        public long bottom;

        public Rect64(long l, long t, long r, long b)
        {
            left = l;
            top = t;
            right = r;
            bottom = b;
        }

        public Rect64(Rect64 rec)
        {
            left = rec.left;
            top = rec.top;
            right = rec.right;
            bottom = rec.bottom;
        }

        public long Width
        {
            readonly get => right - left;
            set => right = left + value;
        }

        public long Height
        {
            readonly get => bottom - top;
            set => bottom = top + value;
        }

        public readonly bool IsEmpty()
        {
            return bottom <= top || right <= left;
        }
        public readonly long2 MidPoint()
        {
            return new long2((left + right) / 2, (top + bottom) / 2);
        }
        public readonly bool Contains(long2 pt)
        {
            return pt.x > left && pt.x < right &&
              pt.y > top && pt.y < bottom;
        }

        public readonly bool Contains(Rect64 rec)
        {
            return rec.left >= left && rec.right <= right &&
              rec.top >= top && rec.bottom <= bottom;
        }
        public readonly bool Intersects(Rect64 rec)
        {
            return (Math.Max(left, rec.left) <= Math.Min(right, rec.right)) &&
              (Math.Max(top, rec.top) <= Math.Min(bottom, rec.bottom));
        }

    }

    ////Note: all clipping operations except for Difference are commutative.
    //public enum ClipType
    //{
    //    None,
    //    Intersection,
    //    Union,
    //    Difference,
    //    Xor
    //};

    public enum PathType
    {
        Subject,
        Clip
    };

    //By far the most widely used filling rules for polygons are EvenOdd
    //and NonZero, sometimes called Alternate and Winding respectively.
    //https://en.wikipedia.org/wiki/Nonzero-rule
    public enum FillRule
    {
        EvenOdd,
        NonZero,
        Positive,
        Negative
    };

    //PointInPolygon
    public enum PipResult
    {
        Inside,
        Outside,
        OnEdge
    };

    public static class InternalClipper
    {
        internal const long MaxInt64 = 9223372036854775807;
        internal const long MaxCoord = MaxInt64 / 4;
        internal const double max_coord = MaxCoord;
        internal const double min_coord = -MaxCoord;
        internal const long Invalid64 = MaxInt64;

        internal const double defaultArcTolerance = 0.25;
        public const double floatingPointTolerance = 1E-12;
        public const double defaultMinimumEdgeLength = 0.1;

        internal static void CheckPrecision(int precision)
        {
            if (precision < -8 || precision > 8)
                Debug.LogError("Precision is out of range.");
        }
        internal static bool IsAlmostZero(double value)
        {
            return (math.abs(value) <= floatingPointTolerance);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CrossProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double)(pt2.x - pt1.x) * (pt3.y - pt2.y) -
                    (double)(pt2.y - pt1.y) * (pt3.x - pt2.x));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DotProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double)(pt2.x - pt1.x) * (pt3.x - pt2.x) +
                    (double)(pt2.y - pt1.y) * (pt3.y - pt2.y));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double CrossProduct(double2 vec1, double2 vec2)
        {
            return (vec1.y * vec2.x - vec2.y * vec1.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double DotProduct(double2 vec1, double2 vec2)
        {
            return (vec1.x * vec2.x + vec1.y * vec2.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long CheckCastInt64(double val)
        {
            if ((val >= max_coord) || (val <= min_coord)) return Invalid64;
            return (long)math.round(val);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GetIntersectPt(long2 ln1a,
            long2 ln1b, long2 ln2a, long2 ln2b, out long2 ip)
        {
            double dy1 = (ln1b.y - ln1a.y);
            double dx1 = (ln1b.x - ln1a.x);
            double dy2 = (ln2b.y - ln2a.y);
            double dx2 = (ln2b.x - ln2a.x);
            double det = dy1 * dx2 - dy2 * dx1;
            if (det == 0.0)
            {
                ip = new long2();
                return false;
            }

            double t = ((ln1a.x - ln2a.x) * dy2 - (ln1a.y - ln2a.y) * dx2) / det;
            if (t <= 0.0) ip = ln1a;
            else if (t >= 1.0) ip = ln1b;
            else ip = new long2(ln1a.x + t * dx1, ln1a.y + t * dy1);
            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GetIntersectPoint(long2 ln1a,
              long2 ln1b, long2 ln2a, long2 ln2b, out long2 ip)
        {
            double dy1 = (ln1b.y - ln1a.y);
            double dx1 = (ln1b.x - ln1a.x);
            double dy2 = (ln2b.y - ln2a.y);
            double dx2 = (ln2b.x - ln2a.x);
            double det = dy1 * dx2 - dy2 * dx1;
            if (det == 0.0)
            {
                ip = new long2();
                return false;
            }
            double t = ((ln1a.x - ln2a.x) * dy2 - (ln1a.y - ln2a.y) * dx2) / det;
            if (t <= 0.0) ip = ln1a;        // ?? check further (see also #568)
            else if (t >= 1.0) ip = ln2a;   // ?? check further
            else ip = new long2(ln1a.x + t * dx1, ln1a.y + t * dy1);
            return true;
        }
        internal static bool SegsIntersect(long2 seg1a,
            long2 seg1b, long2 seg2a, long2 seg2b, bool inclusive = false)
        {
            if (inclusive)
            {
                double res1 = CrossProduct(seg1a, seg2a, seg2b);
                double res2 = CrossProduct(seg1b, seg2a, seg2b);
                if (res1 * res2 > 0) return false;
                double res3 = CrossProduct(seg2a, seg1a, seg1b);
                double res4 = CrossProduct(seg2b, seg1a, seg1b);
                if (res3 * res4 > 0) return false;
                // ensure NOT collinear
                return (res1 != 0 || res2 != 0 || res3 != 0 || res4 != 0);
            }
            else
            {
                return (CrossProduct(seg1a, seg2a, seg2b) *
                  CrossProduct(seg1b, seg2a, seg2b) < 0) &&
                  (CrossProduct(seg2a, seg1a, seg1b) *
                  CrossProduct(seg2b, seg1a, seg1b) < 0);
            }
        }
        public static long2 GetClosestPtOnSegment(long2 offPt,
            long2 seg1, long2 seg2)
        {
            if (seg1.x == seg2.x && seg1.y == seg2.y) return seg1;
            double dx = (seg2.x - seg1.x);
            double dy = (seg2.y - seg1.y);
            double q = ((offPt.x - seg1.x) * dx +
              (offPt.y - seg1.y) * dy) / ((dx * dx) + (dy * dy));
            if (q < 0) q = 0; else if (q > 1) q = 1;
            return new long2(
              seg1.x + math.round(q * dx), seg1.y + math.round(q * dy));
        }

    } //InternalClipperFuncs
} //namespace