using System.Runtime.CompilerServices;
using TextMeshDOTS.HarfBuzz;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.Polybool
{
    public static class PointUtils
    {
        /// <summary>
        /// Returns a positive value if the points a, b, and p occur in counterclockwise order (CCW, p lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(CW, p lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// Result also happens to be twice the signed area of the triangle
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Orient2DFast(double2 a, double2 b, double2 p)
        {
            return (a.x - p.x) * (b.y - p.y) - (a.y - p.y) * (b.x - p.x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCollinear(double2 a, double2 b, double2 p, out double orient2d)
        {
            orient2d = Orient2DFast(a, b, p);
            return math.abs(orient2d) < BezierMath.epsilon1;
        }

        /// <summary> For a given segment (a -> b), determine if it points right (+1) or left (-1). A vertical segment will return 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWindingTowardsBottom(double2 a, double2 b)
        {
            double dx = b.x - a.x;
            if (math.abs(dx) < BezierMath.epsilon1)
                return 0;
            int sign = dx > 0 ? 1 : -1;
            return sign;
        }
        /// <summary> For a given segment (a -> b), determine if it points up (+1) or down (-1). A horizontal segment will return 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWindingTowardsRight(double2 a, double2 b)
        {
            double dy = b.y - a.y;
            if (math.abs(dy) < BezierMath.epsilon1)
                return 0;
            int sign = dy > 0 ? 1 : -1;
            return sign;
        }

        public static bool PointsSame(double2 point1, double2 point2)
        {
            return BezierMath.GenericEquals(point1.x, point2.x) && BezierMath.GenericEquals(point1.y, point2.y);
        }
        /// <summary> returns -1 if point1 is smaller then point2</summary>
        public static int PointsCompare(double2 point1, double2 point2)
        {
            if (BezierMath.GenericEquals(point1.x, point2.x))
            {
                return BezierMath.GenericEquals(point1.y, point2.y) ? 0 : (point1.y < point2.y ? -1 : 1);
            }
            return point1.x < point2.x ? -1 : 1;
        }

        public static double Snap0(double v)
        {
            return math.abs(v) < BezierMath.epsilon1 ? 0 : v;
        }
        public static double Snap01(double v)
        {
            if (math.abs(v) < BezierMath.epsilon1) return 0;
            if (math.abs(1 - v) < BezierMath.epsilon1) return 1;
            return v;
        }   
    }
}