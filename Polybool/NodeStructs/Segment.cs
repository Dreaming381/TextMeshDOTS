using System;
using TextMeshDOTS.HarfBuzz;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{ 
    public struct Segment : IEquatable<Segment>
    {        
        public double2 p0_start;
        public double2 p1_end;
        public FillStatus above;
        public FillStatus below;
        public FillStatus otherAbove;
        public FillStatus otherBelow;
        public bool closed;
        public int windingTopToBottom;     //store here winding of egde crossing vertial ray from top to bottom
        public int windingLeftToRight;    //store here winding of egde crossing horizontal ray from left to right

        public Segment(double2 start, double2 end, bool closed)
        {
            this.p0_start = start;
            this.p1_end = end;
            above = FillStatus.Undefined;
            below = FillStatus.Undefined;
            otherAbove = FillStatus.Undefined;
            otherBelow = FillStatus.Undefined;
            this.closed = closed;
            windingTopToBottom = 0;
            windingLeftToRight = 0;
        }

        public void Split(double2 splitPoint, out Segment right)
        {           
            //generate right Segment
            right = new Segment()
            {
                p0_start = splitPoint,
                p1_end = p1_end,
                above = above,
                below = below,
                //otherAbove = otherAbove, //do NOT copy otherFill to the right segment or the combine phase will fail!!!
                //otherBelow = otherBelow, //do NOT copy otherFill to the right segment or the combine phase will fail!!!
                closed = closed,
                windingTopToBottom = windingTopToBottom,
                windingLeftToRight = windingLeftToRight,
            };
            //update Endpoint of left segment
            p1_end = splitPoint;
        }

        /// <summary>
        /// ATTENTION: provide dx, dy and dist pre-calculated from segment p0_start and p1_end, along with segment p0_start.
        /// We do this to avoid expensive redundant calculation in the algorithm hot path when repeatedly calling this function for same segment.
        /// </summary>
        public static double ProjectPointOntoSegmentLine(double2 p, double2 seg_p0_start, double seg_dx, double seg_dy, double seg_dist)
        {
            double px = p.x - seg_p0_start.x;
            double py = p.y - seg_p0_start.y;
            double dot = px * seg_dx + py * seg_dy;
            return dot / seg_dist;
        }

        public static IntersectionResultType SegmentLineIntersectSegmentLine(
            double2 a0, double2 a1, double2 b0, double2 b1,
            bool allowOutOfRange,
            out double tA1, out double tB1, out double tA2, out double tB2)
        {
            tA1 = tB1 = tA2 = tB2 = default;

            var adx = a1.x - a0.x;
            var ady = a1.y - a0.y;
            var bdx = b1.x - b0.x;
            var bdy = b1.y - b0.y;
            var det = BezierMath.cross2D(adx, ady, bdx, bdy);

            if (math.abs(det) < BezierMath.epsilon1_abs)
            {
                // parallel or coincident
                if (!PointUtils.IsCollinear(a0, a1, b0, out _))
                    return IntersectionResultType.Nothing; // parallel only

                // coincident
                var aDist = adx * adx + ady * ady;
                var b0_OnSeqA = ProjectPointOntoSegmentLine(b0, a0, adx, ady, aDist);
                var b1_OnSeqA = ProjectPointOntoSegmentLine(b1, a0, adx, ady, aDist);
                var tAMin = PointUtils.Snap01(math.min(b0_OnSeqA, b1_OnSeqA));
                var tAMax = PointUtils.Snap01(math.max(b0_OnSeqA, b1_OnSeqA));

                if (tAMax < 0 || tAMin > 1)
                    return IntersectionResultType.Nothing;

                var bDist = adx * adx + ady * ady;
                var a0_OnSeqB = ProjectPointOntoSegmentLine(a0, b0, bdx, bdy, bDist);
                var a1_OnSeqB = ProjectPointOntoSegmentLine(a1, b0, bdx, bdy, bDist);
                var tBMin = PointUtils.Snap01(math.min(a0_OnSeqB, a1_OnSeqB));
                var tBMax = PointUtils.Snap01(math.max(a0_OnSeqB, a1_OnSeqB));

                if (tBMax < 0 || tBMin > 1)
                    return IntersectionResultType.Nothing;

                tA1 = math.max(0, tAMin);
                tB1 = math.max(0, tBMin);
                tA2 = math.min(1, tAMax);
                tB2 = math.min(1, tBMax);
                return IntersectionResultType.Two;
            }

            // intersection at one point
            var dx = a0.x - b0.x;
            var dy = a0.y - b0.y;

            var t1 = BezierMath.cross2D(bdx, bdy, dx, dy) / det;
            var t2 = BezierMath.cross2D(adx, ady, dx, dy) / det;
            t1 = PointUtils.Snap01(t1);
            t2 = PointUtils.Snap01(t2);
            if (!allowOutOfRange && (t1 < 0 || t1 > 1 || t2 < 0 || t2 > 1))
                return IntersectionResultType.Nothing;
            tA1 = t1;
            tB1 = t2;
            return IntersectionResultType.One;
        }        

        public override bool Equals(object obj) => obj is Segment other && Equals(other);

        public bool Equals(Segment other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        public static bool operator ==(Segment e1, Segment e2)
        {
            return e1.GetHashCode() == e2.GetHashCode();
        }
        public static bool operator !=(Segment e1, Segment e2)
        {
            return e1.GetHashCode() != e2.GetHashCode();
        }
        public override int GetHashCode()
        {
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + p0_start.GetHashCode();
            hashCode = hashCode * -1521134295 + p1_end.GetHashCode();
            hashCode = hashCode * -1521134295 + above.GetHashCode();
            hashCode = hashCode * -1521134295 + below.GetHashCode();
            hashCode = hashCode * -1521134295 + otherAbove.GetHashCode();
            hashCode = hashCode * -1521134295 + otherBelow.GetHashCode();
            hashCode = hashCode * -1521134295 + closed.GetHashCode();
            return hashCode;
        }
    }
}