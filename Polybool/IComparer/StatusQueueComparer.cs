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
			var a = seg1.start;
			var b = seg1.end;
			var c = seg2.start;
			var d = seg2.end;

			// orientation of p with resepct to a segment:
			// <0 = CW = left. Because p0 is always left of p1, this means here also "above" 
			// >0 = CCW = right. Because p0 is always left of p1, this means here also "below" 
			// =0 = colinear

			//if seg2 is left of seg1...
			if (c.x < a.x)
			{
                //...then determine oriention of seg1 against seg2(c, d)
                // seg1 is "above" seg2, when it's points are CCW of seg 2...so when orient2d is positive (CCW), we need to return negative!
                if (d.x == c.x)// # vertical segment
                    return -b.y.CompareTo(math.max(c.y, d.y));                //b.y < Math.Max(c.y, d.y)   //# true if seg2 below seg1
                var orient2d = PointUtils.Orient2DAccurate(c, d, a);
				if (math.abs(orient2d) < BezierMath.epsilon1_abs)           // a collinear with seg2 (c,d)?
				{
					orient2d = PointUtils.Orient2DAccurate(c, d, b);
                    if (math.abs(orient2d) < BezierMath.epsilon1_abs)       // b collinear with seg2 (c,d)?
                    {
                        if (seg1.segmentType != seg2.segmentType)           // Segments are collinear, but belong to separate polygons
                            return seg1.segmentType == SegmentType.Primary ? -1 : 1;
                        else
                            return 0;                                       // both a and b are colinear with seg2 (c,d), so segments are coincident
                    }
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
                if (b.x == a.x) //# vertical segment
                    return d.y.CompareTo(math.max(a.y, b.y));                             //d.y > Math.Max(a.y, b.y);//# true if seg1 above seg2
                var orient2d = PointUtils.Orient2DAccurate(a, b, c);
				if (math.abs(orient2d) < BezierMath.epsilon1_abs)           // c collinear with seg1 (a,b)?
				{
					orient2d = PointUtils.Orient2DAccurate(a, b, d);
                    if (math.abs(orient2d) < BezierMath.epsilon1_abs)       // d collinear with seg1 (a,b)?
                    {
                        if(seg1.segmentType != seg2.segmentType)           // Segments are collinear, but belong to separate polygons
                            return seg1.segmentType == SegmentType.Primary ? -1 : 1;
                        else
                            return 0;                                       // both c and d are colinear with seg1 (a,b), so segments are coincident
                    }
                    else                                                // orientation of seg2 (d) with respect to seg1 (a,b).
                        return orient2d < 0 ? -1 : 1;                   // <0 = CW = left of seg1 means "below" (eventA is above eventB, return -1)
				}
				else                                                    // orientation of seg2 (c) with respect to seg1 (a,b).
					return orient2d < 0 ? -1 : 1;                       // <0 = CW = left of seg1 means "below" (eventA is above eventB, return -1)
			}
		}
	}
}