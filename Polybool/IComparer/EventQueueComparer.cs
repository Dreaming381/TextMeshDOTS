using System.Collections.Generic;

namespace TextMeshDOTS.Polybool
{
    public struct EventQueueComparer : IComparer<EventBool>
    {
        public StatusQueueComparer segmentComparer;
        public int sortDirection;

        /// <summary>
        /// Sort event queue decending for a 30% speedup of the algorithm. Adjust indexing into queue accordingly!
        /// </summary>
        public EventQueueComparer(StatusQueueComparer segmentComparer, int sortDirecton)
        {
            // use "1" to sort event queue ascending (most left event is at index 0),
            // "-1" to sort event queue descending (most left event is at index ^1 (last element)),
            // -1 is about 30% faster as it eliminates shifting (=copying) the entire event queue upon each head removal
            this.segmentComparer = segmentComparer;
            this.sortDirection = sortDirecton;
        }
        /// <summary> Sorts events in ASCENDING order on x-axis (and y-axis for same x)  </summary>
        /// <returns>Returns -1 if aEvent is smaller, 1 if bEvent is smaller, 0 if equal </returns>
        public int Compare(EventBool eventA, EventBool eventB)
        {
            // compare the selected points first
            var seg1 = segmentComparer.segments[eventA.segmentID];
            var seg2 = segmentComparer.segments[eventB.segmentID];
            var a1 = seg1.p0_start;
            var a2 = seg1.p1_end;
            var b1 = seg2.p0_start;
            var b2 = seg2.p1_end;
            if (!eventA.isStart)
                (a1, a2) = (a2, a1);
            if (!eventB.isStart)
                (b1, b2) = (b2, b1);

            int comp = PointUtils.PointsCompare(a1, b1);// returns -1 if a1 (aSeq) is smaller b1 (bSeq) (=favour the smaller segment)
            if (comp != 0)                              // the selected points are the same
                return comp * sortDirection;

            // then compare the the other (non-selected) points

            if (PointUtils.PointsSame(a2, b2))          // if the non-selected points are the same too...
                return 0;                               // then the segments are equal

            // check if one segment is a start and the other isn't...
            if (eventA.isStart != eventB.isStart)
                return eventA.isStart ? 1 * sortDirection : -1 * sortDirection;  // returns -1 when a1 is end (aSeq) (=favour end events)

            // abuse DESCENDING IComparer to behave as ASCENDING IComparer
            // by providing segments in flipped order (b - a instead of a - b)
            // so it will return -1 when aSeq is below (bSeq)
            // (=smaller segments on y axis come first)
            return segmentComparer.Compare(eventB, eventA) * sortDirection;
        }
    }
}
