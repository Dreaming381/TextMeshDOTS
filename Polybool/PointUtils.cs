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
        
        /// <summary>
        /// For a given polygon edge, calculate a point that is above this edge. 
        /// Orient2D is used to determine crossing of a ray from this point with the edge. 
        /// If the crossed edge is directed from right to left, we have a positive crossing; otherwise, we have a negative crossing.
        /// https://jeffe.cs.illinois.edu/teaching/comptop/2023/notes/02-winding-number.pdf
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWindingTowardsBottom(double2 a, double2 b)
        {
            var wind = 0;
            //calculate a point 2 units "above" edge
            //feels hacky. Is there a better way?
            double2 p;
            p.x = math.min(b.x, a.x) + (math.abs(b.x - a.x) / 2);
            p.y = math.max(a.y, b.y) + 2;

            var orient2D = Orient2DFast(a, b, p);
            //do not needs this check as we generate point p and it is guarrantied to be between a.x and b.x
            //if (a.x <= p.x && p.x < b.x && orient2D > 0) 
            //    wind = 1;
            //else if (b.x <= p.x && p.x < a.x && orient2D < 0)
            //    wind = -1;

            if (orient2D > 0)
                wind = 1;
            else if (orient2D < 0)
                wind = -1;
            return wind;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWindingTowardsRight(double2 a, double2 b)
        {
            var wind = 0;
            //calculate a point 2 units "left" of edge
            //feels hacky. Is there a better way?
            double2 p;
            p.x = math.max(a.x, b.x) - 2;
            p.y = math.min(b.y, a.y) + (math.abs(b.y - a.y) / 2);

            var orient2D = Orient2DFast(a, b, p);
            //do not needs this check as we generate point p and it is guarrantied to be between a.x and b.x
            //if (a.y <= p.y && p.y < b.y && orient2D > 0)
            //    wind = 1;
            //else if (b.y <= p.y && p.y < a.y && orient2D < 0)
            //    wind = -1;

            if (orient2D > 0)
                wind = 1;
            else if (orient2D < 0)
                wind = -1;

            return wind;
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