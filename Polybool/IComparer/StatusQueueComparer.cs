using System.Collections.Generic;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{
    public struct StatusQueueComparer : IComparer<EventBool>
    {
        public NativeList<Segment> segments;
        public StatusQueueComparer(NativeList<Segment> segments)
        {
            this.segments = segments;
        }

        //assumes segments are already sorted by x coordinate(seg1 is on x-axis before seg2)
        /// <summary> Sorts events in DESCENDING order on y-axis</summary>
        /// <returns>Returns 1 if eventA is smaller, -1 if bEvent is smaller, 0 if equal</returns>
        public int Compare(EventBool eventA, EventBool eventB)
        {
            if (eventA == eventB)
                return 0;
            var seg1 = segments[eventA.segmentID];
            var seg2 = segments[eventB.segmentID];
            var a = seg1.p0_start;
            var b = seg1.p1_end;
            var c = seg2.p0_start;
            var d = seg2.p1_end;

            // orientation of p:
            // <0 = CW = left. Because p0 is always left of p1, this means here also "above" 
            // >0 = CCW = right. Because p0 is always left of p1, this means here also "below" 
            // =0 = colinear

            //if seg2 is left of seg1...
            if (c.x < a.x)
            {
                //...then determine oriention of seg1 against seg2(c, d)
                // seg1 is "above" seg2, when it's points are CCW of seg 2...so when orient2d is positive (CCW), we need to return negative!
                var orient2d = PointUtils.Orient2DFast(c, d, a);
                if (math.abs(orient2d) < BezierMath.epsilon1)           // a collinear with seg2 (c,d)?
                {
                    orient2d = PointUtils.Orient2DFast(c, d, b);
                    if (math.abs(orient2d) < BezierMath.epsilon1)       // b collinear with seg2 (c,d)?
                        return 0;                                       // both a and b are colinear with seg2 (c,d), so segments are coincident
                    else
                        return orient2d < 0 ? 1 : -1;                   // orientation of seg1 (b) with respect to seg2 (c,d). <0 = CW = left of seg2 means "above"
                }
                else
                    return orient2d < 0 ? 1 : -1;                       // orientation of seg1 (a) with respect to seg2 (c,d). <0 = CW = left of seg2 means "above"
            }
            else
            {
                //...determine oriention of seg2 against seg1(a, b)
                // seg1 is "above" seg2, when seg2 points are are CW of seg 1..which is directly the result of orient2D
                var orient2d = PointUtils.Orient2DFast(a, b, c);
                if (math.abs(orient2d) < BezierMath.epsilon1)           // c collinear with seg1 (a,b)?
                {
                    orient2d = PointUtils.Orient2DFast(a, b, d);
                    if (math.abs(orient2d) < BezierMath.epsilon1)       // d collinear with seg1 (a,b)?
                        return 0;                                       // both c and d are colinear with seg1 (a,b), so segments are coincident
                    else
                        return orient2d < 0 ? -1 : 1;                   // orientation of seg2 (d) with respect to seg1 (a,b). <0 = CW = left of seg1 means "below"
                }
                else
                    return orient2d < 0 ? -1 : 1;                       // orientation of seg2 (c) with respect to seg1 (a,b). <0 = CW = left of seg1 means "below"
            }
        }
    }
}
