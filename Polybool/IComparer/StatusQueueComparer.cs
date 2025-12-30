using System;
using System.Collections.Generic;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;

namespace TextMeshDOTS.Polybool
{
    public struct StatusQueueComparer : IComparer<EventBool>
    {
        public NativeList<Segment> segments;
        public StatusQueueComparer(NativeList<Segment> segments)
        {
            this.segments = segments;
        }

        /// <summary> Sorts events in DESCENDING order on y-axis</summary>
        /// <returns>Returns -1 if eventA is above eventB, 1 if eventB is above eventB, 0 if equal</returns>
        public int Compare(EventBool eventA, EventBool eventB)
        {
            return StatusQueueComparerClass.Compare(eventA, eventB, segments);
        }
    }
    public static class StatusQueueComparerClass
    {
        public static int BinarySearch(NativeList<EventBool> statusQueue, EventBool ev, NativeList<Segment> segments)
        {
            int lo = 0;
            int hi = statusQueue.Length - 1;
            while (lo <= hi)
            {
                int i = (int) (((uint) hi + (uint) lo) >> 1);
                int c = Compare(ev, statusQueue[i], segments);
                if (c == 0)
                    return i;
                else if (c > 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }
            return ~lo;
        }

        /// <summary> Sorts events in DESCENDING order on y-axis</summary>
        /// <returns>Returns -1 if eventA is above eventB, 1 if eventB is above eventB, 0 if equal</returns>
        public static int Compare(EventBool eventA, EventBool eventB, NativeList<Segment> segments)
        {
            if (eventA == eventB)
                return 0;
            var seg1 = segments[eventA.segmentID];
            var seg2 = segments[eventB.segmentID];
            var a1 = seg1.start;
            var a2 = seg1.end;
            var b1 = seg2.start;
            var b2 = seg2.end;
            var a1_p0 = seg1.p0;
            var a2_p1 = seg1.p1;
            var b1_p0 = seg2.p0;
            var b2_p1 = seg2.p1;

            // orientation of p with respect to a segment:
            // <0 = CW = left. Because p0 is always left of p1, this means here also "above" 
            // >0 = CCW = right. Because p0 is always left of p1, this means here also "below" 
            // =0 = colinear

            //if seg2 is left of seg1...            
            if (Rational.CompareX(a1, b1, ref seg1, ref seg2) > 0)
            {
                //...then determine oriention of seg1 against seg2(c, d)
                // seg1 is "above" seg2, when it's points are CCW of seg 2...so when orient2d is positive (CCW), we need to return negative!                
                var orient2d = PointUtils.Orient2DParamPoint(b1_p0, b2_p1, a1, ref seg1);
                if (Math.Abs(orient2d) < BezierMath.epsilon1_abs)           // a collinear with seg2 (c,d)?
                {
                    orient2d = PointUtils.Orient2DParamPoint(b1_p0, b2_p1, a2, ref seg1);
                    if (Math.Abs(orient2d) < BezierMath.epsilon1_abs)       // b collinear with seg2 (c,d)?
                        return 0;                                       // both a and b are colinear with seg2 (c,d), so segments are coincident
                    else                                                // orientation of seg1 (b) with respect to seg2 (c,d).
                        return orient2d < 0 ? 1 : -1;                   // <0 = CW = left of seg2 means "above" (eventA is below eventB, return 1)
                }
                else                                                    // orientation of seg1 (a) with respect to seg2 (c,d).
                    return orient2d < 0 ? 1 : -1;                       // <0 = CW = left of seg2 means "above" (eventA is below eventB, return 1)
            }
            else
            {
                //...determine oriention of seg2 against seg1(a, b)
                // seg1 is "above" seg2, when seg2 points are are CW of seg 1..which is directly the result of orient2D                
                var orient2d = PointUtils.Orient2DParamPoint(a1_p0, a2_p1, b1, ref seg2);
                if (Math.Abs(orient2d) < BezierMath.epsilon1_abs)           // c collinear with seg1 (a,b)?
                {
                    orient2d = PointUtils.Orient2DParamPoint(a1_p0, a2_p1, b2, ref seg2);
                    if (Math.Abs(orient2d) < BezierMath.epsilon1_abs)       // d collinear with seg1 (a,b)?
                        return 0;                                       // both c and d are colinear with seg1 (a,b), so segments are coincident
                    else                                                // orientation of seg2 (d) with respect to seg1 (a,b).
                        return orient2d < 0 ? -1 : 1;                   // <0 = CW = left of seg1 means "below" (eventA is above eventB, return -1)
                }
                else                                                    // orientation of seg2 (c) with respect to seg1 (a,b).
                    return orient2d < 0 ? -1 : 1;                       // <0 = CW = left of seg1 means "below" (eventA is above eventB, return -1)
            }
        }
    }
}