using System;
using System.Diagnostics;
using TextMeshDOTS.HarfBuzz;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{
    [DebuggerDisplay("{start} {end}")]
    public struct Segment : IEquatable<Segment>
    {
        public SegmentType segmentType;
        public long2 start;
        public long2 end;
        public int windingTopToBottom;     //store here winding of egde crossing vertial ray from top to bottom
        public int windingLeftToRight;    //store here winding of egde crossing horizontal ray from left to right
        byte _boolField;
        public bool fillAbove
        {
            get { return Utils.GetBit(_boolField, 0); }
            set { _boolField = Utils.SetBit(_boolField, 0, value); }
        }
        public bool fillBelow
        {
            get { return Utils.GetBit(_boolField, 1); }
            set { _boolField = Utils.SetBit(_boolField, 1, value); }
        }
        public bool fillOtherAbove
        {
            get { return Utils.GetBit(_boolField, 2); }
            set { _boolField = Utils.SetBit(_boolField, 2, value); }
        }
        public bool fillOtherBelow
        {
            get { return Utils.GetBit(_boolField, 3); }
            set { _boolField = Utils.SetBit(_boolField, 3, value); }
        }
        public bool myFillSet
        {
            get { return Utils.GetBit(_boolField, 4); }
            set { _boolField = Utils.SetBit(_boolField, 4, value); }
        }
        public bool otherFillSet
        {
            get { return Utils.GetBit(_boolField, 5); }
            set { _boolField = Utils.SetBit(_boolField, 5, value); }
        }
        public bool closed
        {
            get { return Utils.GetBit(_boolField, 6); }
            set { _boolField = Utils.SetBit(_boolField, 6, value); }
        }
        public bool inResults
        {
            get { return Utils.GetBit(_boolField, 7); }
            set { _boolField = Utils.SetBit(_boolField, 7, value); }
        }


        public Segment(long2 start, long2 end, SegmentType segmentType, bool closed)
        {
            this.start = start;
            this.end = end;
            _boolField = 0;
            this.segmentType = segmentType;
            windingTopToBottom = 0;
            windingLeftToRight = 0;
            myFillSet = false;
            otherFillSet = false;
            fillAbove = false;
            fillBelow = false;
            fillOtherAbove = false;
            fillOtherBelow = false;
            this.closed = closed;
        }

        public void Split(long2 splitPoint, out Segment right)
        {
            //generate right Segment
            right = new Segment()
            {
                start = splitPoint,
                end = end,
                myFillSet = myFillSet,
                fillAbove = fillAbove,
                fillBelow = fillBelow,
                //otherAbove = otherAbove, //do NOT copy otherFill to the right segment or the combine phase will fail!!!
                //otherBelow = otherBelow, //do NOT copy otherFill to the right segment or the combine phase will fail!!!
                segmentType = segmentType,
                closed = closed,
                windingTopToBottom = windingTopToBottom,
                windingLeftToRight = windingLeftToRight,
            };
            //update Endpoint of left segment
            end = splitPoint;
        }

        /// <summary>
        /// ATTENTION: provide dx, dy and dist pre-calculated from segment p0_start and p1_end, along with segment p0_start.
        /// We do this to avoid expensive redundant calculation in the algorithm hot path when repeatedly calling this function for same segment.
        /// </summary>
        public static double ProjectPointOntoSegmentLine(long2 p, long2 seg_p0_start, double seg_dx, double seg_dy, double seg_dist)
        {
            double px = p.x - seg_p0_start.x;
            double py = p.y - seg_p0_start.y;
            double dot = px * seg_dx + py * seg_dy;
            return dot / seg_dist;
        }

        public static IntersectionResultType SegmentLineIntersectSegmentLine(
            long2 a0, long2 a1, long2 b0, long2 b1,
            bool allowOutOfRange,
            out double tA1, out double tB1, out double tA2, out double tB2)
        {
            tA1 = tB1 = tA2 = tB2 = default;

            var adx = a1.x - a0.x;
            var ady = a1.y - a0.y;
            var bdx = b1.x - b0.x;
            var bdy = b1.y - b0.y;
            var det = PointUtils.CrossProduct(adx, ady, bdx, bdy);

            if (math.abs(det) < BezierMath.epsilon1_abs)
            {
                // parallel or coincident
                if (!PointUtils.IsCollinear(a0, b0, a1))
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

            if (dx == 0 && dy == 0)
                return IntersectionResultType.Nothing;

            var t1 = PointUtils.CrossProduct(bdx, bdy, dx, dy) / det;
            var t2 = PointUtils.CrossProduct(adx, ady, dx, dy) / det;
            t1 = PointUtils.Snap01(t1);
            t2 = PointUtils.Snap01(t2);
            if (!allowOutOfRange && (t1 < 0 || t1 > 1 || t2 < 0 || t2 > 1))
                return IntersectionResultType.Nothing;
            tA1 = t1;
            tB1 = t2;
            return IntersectionResultType.One;
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is Segment p)
                return this == p;
            else
                return false;
        }

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
            return HashCode.Combine(start, end, _boolField);
        }
    }
}