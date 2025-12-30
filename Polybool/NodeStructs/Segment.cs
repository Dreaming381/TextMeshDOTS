using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TextMeshDOTS.Polybool
{
    [DebuggerDisplay("{StartPoint} {EndPoint}")]
    public struct Segment : IEquatable<Segment>
    {

        public readonly long2 p0;                  // ORIGINAL exact endpoints (do not modify after construction)
        public readonly long2 p1;                  // ORIGINAL exact endpoints (do not modify after construction)
        public readonly long2 dxy => p1 - p0;
        public readonly long dx => p1.x - p0.x;
        public readonly long dy => p1.y - p0.y;
        public Rational start;              // Parametric represetation of start point: p(start) = p0 + start * (p1 - p0)
        public Rational end;                // Parametric represetation of end point: p(end) = p0 + end * (p1 - p0)

        public int windingTopToBottom;      //store here winding of egde crossing vertial ray from top to bottom
        public int windingLeftToRight;      //store here winding of egde crossing horizontal ray from left to right
        ushort _boolField;
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
        public bool isPrimary
        {
            get { return Utils.GetBit(_boolField, 8); }
            set { _boolField = Utils.SetBit(_boolField, 8, value); }
        }
        public long2 GetPoint(Rational rational)
        {
            return new long2(
                p0.x + (double) (dx * rational.num) / rational.den,
                p0.y + (double) (dy * rational.num) / rational.den
            );
        }
        // Exact evaluation (only use when coordinates are *really* needed
        public readonly long2 StartPoint
        {
            get
            {
                return new long2(
                    p0.x + (double) (dx * start.num) / start.den,
                    p0.y + (double) (dy * start.num) / start.den
                );
            }
        }
        public readonly long2 EndPoint
        {
            get
            {
                return new long2(
                    p0.x + (double) (dx * end.num) / end.den,
                    p0.y + (double) (dy * end.num) / end.den
                );
            }
        }

        public Segment(long2 segStart, long2 segEnd, Rational intervalStart, Rational intervalEnd, bool isPrimary, bool closed)
        {
            p0 = segStart;
            p1 = segEnd;
            start = intervalStart;
            end = intervalEnd;
            _boolField = 0;
            windingTopToBottom = 0;
            windingLeftToRight = 0;
            myFillSet = false;
            otherFillSet = false;
            fillAbove = false;
            fillBelow = false;
            fillOtherAbove = false;
            fillOtherBelow = false;
            this.closed = closed;
            this.isPrimary = isPrimary;
        }
        public Segment(Segment segment, bool fillAbove, bool fillBelow)
        {
           p0 = segment.p0;
            p1 = segment.p1;
            start = segment.start;
            end = segment.end;
            _boolField = segment._boolField;
            windingTopToBottom = segment.windingTopToBottom;
            windingLeftToRight = segment.windingLeftToRight;
            myFillSet = true;            
            this.fillAbove = fillAbove;
            this.fillBelow = fillBelow;
            otherFillSet = true;
            fillOtherAbove = false;
            fillOtherBelow = false;
            closed = segment.closed;
            isPrimary = true;
        }

        public Segment(Segment segment, Rational intervalStart)
        {
            p0 = segment.p0;
            p1 = segment.p1;
            start = intervalStart;
            end = segment.end;
            _boolField = segment._boolField;
            windingTopToBottom = segment.windingTopToBottom;
            windingLeftToRight = segment.windingLeftToRight;
            myFillSet = segment.myFillSet;
            fillAbove = segment.fillAbove;
            fillBelow = segment.fillBelow;
            otherFillSet = false;	//do NOT copy otherFill to the right segment or the combine phase will fail!!!
            fillOtherAbove = false; //do NOT copy otherFill to the right segment or the combine phase will fail!!!
            fillOtherBelow = false; //do NOT copy otherFill to the right segment or the combine phase will fail!!!
            closed = segment.closed;
            isPrimary = segment.isPrimary;
        }

        public void Split(Rational ip, out Segment right)
        {
            //generate right Segment
            right = new Segment(this, ip);

            //update Endpoint of left segment
            end = ip;
        }

        public static IntersectionResultType SegmentLineIntersectSegmentLine(ref Segment seg1, ref Segment seg2, out Rational tA1, out Rational tB1, out Rational tA2, out Rational tB2)
        {
            var a0 = seg1.p0;
            var a1 = seg1.p1;
            var aMin = seg1.start;
            var aMax = seg1.end;
            var b0 = seg2.p0;
            var b1 = seg2.p1;
            var bMin = seg2.start;
            var bMax = seg2.end;           

            tA1 = tA2 = tB1 = tB2 = default;

            long adx = a1.x - a0.x;
            long ady = a1.y - a0.y;
            long bdx = b1.x - b0.x;
            long bdy = b1.y - b0.y;

            long det = PointUtils.CrossProduct(adx, ady, bdx, bdy);

            // =========================================================
            // PARALLEL OR COLLINEAR
            // =========================================================
            if (det == 0)
            {
                if (!PointUtils.IsCollinear(a0, b0, a1))
                    return IntersectionResultType.Nothing;

                // --- Project B endpoints onto A ---
                long aLen2 = adx * adx + ady * ady;

                Rational b0onA = Rational.ProjectPointOntoSegmentLine(b0, a0, adx, ady, aLen2);
                Rational b1onA = Rational.ProjectPointOntoSegmentLine(b1, a0, adx, ady, aLen2);

                Rational aOverlapMin = Rational.Max(Rational.Min(b0onA, b1onA), aMin);
                Rational aOverlapMax = Rational.Min(Rational.Max(b0onA, b1onA), aMax);

                if (aOverlapMin.CompareTo(aOverlapMax) > 0)
                    return IntersectionResultType.Nothing;

                // --- Project A endpoints onto B ---
                long bLen2 = bdx * bdx + bdy * bdy;

                Rational a0onB = Rational.ProjectPointOntoSegmentLine(a0, b0, bdx, bdy, bLen2);
                Rational a1onB = Rational.ProjectPointOntoSegmentLine(a1, b0, bdx, bdy, bLen2);

                Rational bOverlapMin = Rational.Max(Rational.Min(a0onB, a1onB), bMin);
                Rational bOverlapMax = Rational.Min(Rational.Max(a0onB, a1onB), bMax);

                if (bOverlapMin.CompareTo(bOverlapMax) > 0)
                    return IntersectionResultType.Nothing;

                // --- Determine if overlap is one point or two ---
                if (aOverlapMin.CompareTo(aOverlapMax) == 0)
                {
                    // Touching at exactly one point
                    tA1 = aOverlapMin;
                    tB1 = bOverlapMin;
                    return IntersectionResultType.One;
                }

                // Proper overlapping segment
                tA1 = aOverlapMin;
                tA2 = aOverlapMax;
                tB1 = bOverlapMin;
                tB2 = bOverlapMax;
                return IntersectionResultType.Two;
            }

            // =========================================================
            // SINGLE INTERSECTION POINT
            // =========================================================
            long dx = a0.x - b0.x;
            long dy = a0.y - b0.y;

            long numA = PointUtils.CrossProduct(bdx, bdy, dx, dy);
            long numB = PointUtils.CrossProduct(adx, ady, dx, dy);

            Rational tA = new Rational(numA, det);
            Rational tB = new Rational(numB, det);

            if (!Rational.InRangeInclusive(tA, aMin, aMax) || !Rational.InRangeInclusive(tB, bMin, bMax))
                return IntersectionResultType.Nothing;

            tA1 = tA;
            tB1 = tB;
            return IntersectionResultType.One;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> Returns -1 if parametric point1 is smaller than parametric point2</summary>
        public static int Compare(in long2 pa, in long2 dpa, in Rational tA, in long2 pb, in long2 dpb, in Rational tB)
        {
            var compX = CompareCoord(pa.x, dpa.x, tA, pb.x, dpb.x, tB);
            if (compX == 0)
                return CompareCoord(pa.y, dpa.y, tA, pb.y, dpb.y, tB);
            return compX;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> Returns -1 if parametric point1 is smaller than parametric point2</summary>
        public static int CompareCoord(in long p0a, in long da, in Rational tA, in long p0b, in long db, in Rational tB)
        {
            // (p0 + t * dp) but expanded and cross-multiplied
            // (p0a * a.den + a.nom * dpa) * db  vs  (p0b * b.den + b.nom * dpb) * da

            //checked
            //{
            //    // this will overflow on large polygons like clipper polygon.txt test 114
            //    // either typecast to double before multiplying or use exact 128bit interger math
            //    long lhs1 = (p0a * tA.den + tA.num * da) * tB.den;
            //    long rhs1 = (p0b * tB.den + tB.num * db) * tA.den;

            //    //return lhs < rhs ? -1 : lhs > rhs ? 1 : 0;
            //}

            //var p0a_da = PointUtils128.Mul128(p0a, tA.den);
            //var na_da = PointUtils128.Mul128(tA.num, da);
            //var p0a_da_na_da = PointUtils128.Add128(p0a_da, na_da);
            //var lhs = PointUtils128.Mul128x64(p0a_da_na_da, tB.den);


            //var p0b_db = PointUtils128.Mul128(p0b, tB.den);
            //var nb_dpb = PointUtils128.Mul128(tB.num, db);
            //var p0b_db_nb_dpb = PointUtils128.Add128(p0b_db, nb_dpb);
            //var rhs = PointUtils128.Mul128x64(p0b_db_nb_dpb, tA.den);



            // typecast to double to avoid potential int overflow (e.g. in clipper polygons.txt test 114)
            double lhs = (((double) p0a * tA.den + (double) tA.num * da) * tB.den);
            double rhs = (((double) p0b * tB.den + (double) tB.num * db) * tA.den);

            return lhs < rhs ? -1 : lhs > rhs ? 1 : 0;
        }
        public override bool Equals(object obj)
        {
            return obj is Segment other && Equals(other);
        }
        public bool Equals(Segment other)
        {
            return p0 == other.p0 && p1 == other.p1 &&
                start == other.start && end == other.end &&
                isPrimary == other.isPrimary && 
                windingTopToBottom == other.windingTopToBottom &&  
                windingLeftToRight == other.windingLeftToRight && 
                _boolField == other._boolField;
        }

        public static bool operator ==(Segment e1, Segment e2)
        {
            return e1.p0 == e2.p0 && e1.p1 == e2.p1 &&
                e1.start == e2.start && e1.end == e2.end &&
                e1.isPrimary == e2.isPrimary &&
                e1.windingTopToBottom == e2.windingTopToBottom &&
                e1.windingLeftToRight == e2.windingLeftToRight &&
                e1._boolField == e2._boolField;
        }
        public static bool operator !=(Segment e1, Segment e2)
        {
            return !(e1==e2);
        }
        public override int GetHashCode()
        {
            //return HashCode.Combine(p0, p1, _boolField);
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + p0.GetHashCode();
            hashCode = hashCode * -1521134295 + p1.GetHashCode();
            hashCode = hashCode * -1521134295 + start.GetHashCode();
            hashCode = hashCode * -1521134295 + end.GetHashCode();
            hashCode = hashCode * -1521134295 + _boolField.GetHashCode();
            return hashCode;
        }
    }
}