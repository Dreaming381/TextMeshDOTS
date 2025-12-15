using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.Polybool
{
    public struct Intersecter
    {
        public bool selfIntersection;
        NativeList<EventBool> eventQueue;
        NativeList<EventBool> statusQueue;
        NativeList<Segment> segments;
        EventQueueComparer eventQueueComparer;
        StatusQueueComparer statusQueueComparer;
        FillRule fillRule;

        public Intersecter(bool selfIntersection, int size, FillRule fillRule)
        {
            this.selfIntersection = selfIntersection;
            this.fillRule = fillRule;
            eventQueue = new NativeList<EventBool>(2 * size, Allocator.Temp);
            statusQueue = new NativeList<EventBool>(64, Allocator.Temp);
            segments = new NativeList<Segment>(size, Allocator.Temp);
            eventQueueComparer = default;
            statusQueueComparer = default;   
        }
        public void Reset(bool selfIntersection)
        {
            this.selfIntersection = selfIntersection;
            eventQueue.Clear();
            statusQueue.Clear();
            segments.Clear();
        }
        #region initialize events
        public void AddRegion(Polygon region, int start, int end)
        {
            double2 from;
            double2 to = region.nodes[end - 1];
            for (int i = start; i < end; i++)
            {
                from = to;
                to = region.nodes[i];

                int forward = PointUtils.PointsCompare(from, to);
                if (forward == 0)
                    continue; // points are equal, so we have a zero-length segment

                var segNew = forward < 0 ? new Segment(from, to, true) : new Segment(to, from, true);
                if (fillRule == FillRule.NonZero)
                {
                    segNew.windingTopToBottom = PointUtils.GetWindingTowardsBottom(from, to);
                    segNew.windingLeftToRight = PointUtils.GetWindingTowardsRight(from, to);
                }

                var segmentID = segments.Length;
                segments.Add(segNew);                
                CreateEvents(segmentID, true, out EventBool evStart, out EventBool evEnd);
                //just add, sort at the end in one go to avoid array shifts
                eventQueue.Add(evStart);
                eventQueue.Add(evEnd);
                //AddEvent(evStart);
                //AddEvent(evEnd);
            }
            statusQueueComparer = new StatusQueueComparer(segments);
            eventQueueComparer = new EventQueueComparer(statusQueueComparer, -1);
            eventQueue.Sort(eventQueueComparer);
            //Utils.WriteSegmentsToFile("segments.txt", segments);
        }
        public void AddSegments(
            NativeList<Segment> segments1, bool primary,
            NativeList<Segment> segments2, bool secondary)
        {
            
            for (int i = 0, end = segments1.Length; i < end; i++)
            {
                var segmentID = segments.Length;
                segments.Add(segments1[i]);
                CreateEvents(segmentID, primary, out EventBool evStart, out EventBool evEnd);
                eventQueue.Add(evStart);//just add, sort at the end in one go to avoid array shifts
                eventQueue.Add(evEnd);
                //AddEvent(evStart);
                //AddEvent(evEnd);
            }
            for (int i = 0, end = segments2.Length; i < end; i++)
            {
                var segmentID = segments.Length;
                segments.Add(segments2[i]);
                CreateEvents(segmentID, secondary, out EventBool evStart, out EventBool evEnd);
                eventQueue.Add(evStart);//just add, sort at the end in one go to avoid array shifts
                eventQueue.Add(evEnd);
                //AddEvent(evStart);
                //AddEvent(evEnd);
            }
            statusQueueComparer.segments = segments;
            eventQueueComparer.segmentComparer = statusQueueComparer;
            eventQueue.Sort(eventQueueComparer);
        }
        #endregion

        void CreateEvents(int segmentID, bool primary, out EventBool evStart, out EventBool evEnd)
        {
            evStart = new EventBool(true, segmentID, primary);
            evEnd = new EventBool(false, segmentID, primary);
        }
        void DivideEvent(EventBool ev, double t, double2 p)
        {
            var rightSegmentID = segments.Length;
            var seg = segments[ev.segmentID];
            seg.Split(p, out Segment right);
            segments[ev.segmentID] = seg;
            segments.Add(right);

            // slides an end backwards
            //   (start)------------(end)    to:
            //   (start)---(end)

            //remove and add the "other" event of the left segment, because new endpoint will cause new position in event queue
            var evOther = ev.other;
            eventQueue.RemoveAt(eventQueue.IndexOf(evOther));
            AddEvent(evOther);

            //create and add the right segment events to event queue
            CreateEvents(rightSegmentID, ev.primary, out EventBool evStart, out EventBool evEnd);
            AddEvent(evStart);
            AddEvent(evEnd);
        }
        void AddEvent(EventBool ev)
        {
            var eventIndexInEvents = eventQueue.BinarySearch(ev, eventQueueComparer);
            eventIndexInEvents = eventIndexInEvents < 0 ? ~eventIndexInEvents : eventIndexInEvents;
            eventQueue.InsertRange(eventIndexInEvents, 1);
            eventQueue[eventIndexInEvents] = ev;            
        }
        EventBool CheckIntersection(EventBool ev1, EventBool ev2)
        {
            // returns the segment equal to ev1, or false if nothing equal
            Segment seg1 = segments[ev1.segmentID];
            Segment seg2 = segments[ev2.segmentID];
            double2 a0 = seg1.p0_start;
            double2 a1 = seg1.p1_end;
            double2 b0 = seg2.p0_start;
            double2 b1 = seg2.p1_end;

            var intersectResult = Segment.SegmentLineIntersectSegmentLine(a0, a1, b0, b1, false, out double tA1, out double tB1, out double tA2, out double tB2);

            if (intersectResult == IntersectionResultType.Nothing)
            {
                // no intersections
                return EventBool.Empty;
            }
            else if (intersectResult == IntersectionResultType.Two)
            {
                // segments are parallel or coincident

                if ((tA1 == 1 && tA2 == 1 && tB1 == 0 && tB2 == 0) ||
                    (tA1 == 0 && tA2 == 0 && tB1 == 1 && tB2 == 1))
                    return EventBool.Empty; // segments touch at endpoints... no intersection

                if (tA1 == 0 && tA2 == 1 && tB1 == 0 && tB2 == 1)
                    return ev2; // segments are exactly equal

                if (tA1 == 0 && tB1 == 0)
                {
                    if (tA2 == 1)
                    {
                        //  (a0)---(a1)
                        //  (b0)----------(b1)
                        DivideEvent(ev2, tB2, a1);
                    }
                    else
                    {
                        //  (a0)----------(a1)
                        //  (b0)---(b1)
                        DivideEvent(ev1, tA2, b1);
                    }
                    return ev2;
                }
                else if (tB1 > 0 && tB1 < 1)
                {
                    if (tA2 == 1 && tB2 == 1)
                    {
                        //         (a0)---(a1)
                        //  (b0)----------(b1)
                        DivideEvent(ev2, tB1, a0);
                    }
                    else
                    {
                        // make a1 equal to b1
                        if (tA2 == 1)
                        {
                            //         (a0)---(a1)
                            //  (b0)-----------------(b1)
                            DivideEvent(ev2, tB2, a1);
                        }
                        else
                        {
                            //         (a0)----------(a1)
                            //  (b0)----------(b1)
                            DivideEvent(ev1, tA2, b1);
                        }
                        //         (a0)---(a1)
                        //  (b0)----------(b1)
                        DivideEvent(ev2, tB1, a0);
                    }
                }
                return EventBool.Empty;
            }
            else if (intersectResult == IntersectionResultType.One)
            {
                // process a single intersection

                // even though *in theory* seg1.data.point(tA) === seg2.data.point(tB), that isn't exactly
                // correct in practice because intersections aren't exact... so we need to calculate a single
                // intersection point that everyone can share
                var p = tB1 == 0 ? seg2.p0_start :
                        tB1 == 1 ? seg2.p1_end :
                        tA1 == 0 ? seg1.p0_start :
                        tA1 == 1 ? seg1.p1_end :
                        math.lerp(seg1.p0_start, seg1.p1_end, tA1);

                // is A divided between its endpoints? (exclusive)
                if (tA1 > 0 && tA1 < 1)
                    DivideEvent(ev1, tA1, p);

                // is B divided between its endpoints? (exclusive)
                if (tB1 > 0 && tB1 < 1)
                    DivideEvent(ev2, tB1, p);

                return EventBool.Empty;
            }

            Debug.LogError("PolyBool: Unknown intersection type");
            return EventBool.Empty;
        }
        public NativeList<Segment> Calculate(bool primaryPolyInverted, bool secondaryPolyInverted)
        {            
            var result = new NativeList<Segment>(segments.Length, Allocator.Temp);
            while (eventQueue.Length > 0)
            {
                var ev = eventQueue[^1]; //eventQueue[0] when sorted ascending (most left event is at index 0)
                var evSegment = segments[ev.segmentID];
                if (ev.isStart)
                {                   
                    var eventIndexInStatus = statusQueue.BinarySearch(ev, statusQueueComparer);
                    eventIndexInStatus = eventIndexInStatus < 0 ? ~eventIndexInStatus : eventIndexInStatus;
                    EventBool above, below;
                    above = eventIndexInStatus <= 0 ? EventBool.Empty : statusQueue[eventIndexInStatus - 1];
                    below = eventIndexInStatus < statusQueue.Length ? statusQueue[eventIndexInStatus] : EventBool.Empty;

                    // calculate fill flags                    
                    if (selfIntersection)
                    {                        
                        if (fillRule == FillRule.EvenOdd)
                        {
                            // FillRule.EvenOdd
                            bool toggle;
                            // (1) determine if the edge is a "toggling edge"
                            if (evSegment.below == FillStatus.Undefined)
                                toggle = evSegment.closed;                             // new segment. toggle if we're part of a closed path
                            else
                                toggle = evSegment.above != evSegment.below;           // segment resulted from division, and is toggling when above and below fill are not the same

                            // (2) determine below fill
                            if (below == EventBool.Empty)
                                evSegment.below = FillStatus.NotFilled;                // no segment is below us, so not filled
                            else
                                evSegment.below = segments[below.segmentID].above;     // copy the above fill from the segment below 

                            // (3) determine above fill
                            if (toggle)
                                evSegment.above = evSegment.below ^ FillStatus.ToggleMask;//above fill is opposite of below fill
                            else
                                evSegment.above = evSegment.below;                        //above fill is same as below fill
                        }
                        else 
                        {
                            // FillRule.NonZero, FillRule.Positive, FillRule.Negative: derive fill annotation from winding
                            // NonZero: winding !=0 means "inside/filled" and winding = 0 means "outside" "not filled"
                            // Positive: winding >0 means "inside/filled", otherwise it means "outside" "not filled"
                            // Negative: winding <0 means "inside/filled", otherwise it means "outside" "not filled"

                            // (1) determine winding below current event segment by summing all windings from eventIndexInStatus towards bottom of status
                            int windingBelow = 0, windingAbove=0;
                            for (int i = eventIndexInStatus, end = statusQueue.Length; i < end; i++)
                                windingBelow += segments[statusQueue[i].segmentID].windingTopToBottom;

                            // (2) determine winding above current event segment. Simply add "winding" from event segment.
                            // For a vertical edge, the winding does NOT change along y axis,
                            // but it does change along x-axis, so use "windingLeftToRight" value instead
                            windingAbove = evSegment.windingTopToBottom == 0 ? windingBelow + evSegment.windingLeftToRight : windingBelow + evSegment.windingTopToBottom;

                            switch(fillRule)
                            {
                                case FillRule.NonZero:
                                    evSegment.below = windingBelow != 0 ? FillStatus.Filled : FillStatus.NotFilled;
                                    evSegment.above = windingAbove != 0 ? FillStatus.Filled : FillStatus.NotFilled;
                                    break;
                                case FillRule.Positive:
                                    evSegment.below = windingBelow > 0 ? FillStatus.Filled : FillStatus.NotFilled;
                                    evSegment.above = windingAbove > 0 ? FillStatus.Filled : FillStatus.NotFilled;
                                    break;
                                case FillRule.Negative:
                                    evSegment.below = windingBelow < 0 ? FillStatus.Filled : FillStatus.NotFilled;
                                    evSegment.above = windingAbove < 0 ? FillStatus.Filled : FillStatus.NotFilled;
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // now we fill in any missing transition information, since we are all-knowing at this point                        
                        if (evSegment.otherAbove == FillStatus.Undefined && evSegment.otherBelow == FillStatus.Undefined)
                        {
                            // if we don't have other information, then we need to figure out if we're inside the other polygon
                            FillStatus inside;
                            if (below == EventBool.Empty)
                            {
                                // if nothing is below us, then we're not filled
                                inside = FillStatus.NotFilled;
                            }
                            else
                            {
                                // otherwise, something is below us
                                // so copy the below segment's other polygon's above
                                var belowSegment = segments[below.segmentID];
                                if (ev.primary == below.primary)
                                {
                                    if (belowSegment.otherAbove == FillStatus.Undefined)
                                        throw new Exception("PolyBool: Unexpected state of otherFill (null)");
                                    inside = belowSegment.otherAbove;
                                }
                                else
                                    inside = belowSegment.above;
                            }
                            evSegment.otherAbove = inside;
                            evSegment.otherBelow = inside;
                        }
                    }
                    segments[ev.segmentID] = evSegment;

                    EventBool eve = EventBool.Empty;
                    if (above != EventBool.Empty)
                    {
                        eve = CheckIntersection(ev, above);
                        if (eve == EventBool.Empty && below != EventBool.Empty)
                            eve = CheckIntersection(ev, below);
                    }

                    if (eve != EventBool.Empty)
                    {
                        // ev and eve are equal
                        // we'll keep eve and throw away ev

                        // merge ev.seg"s fill information into eve.seg

                        if (selfIntersection)
                        {
                            bool toggle; // are we a toggling edge?
                            if (evSegment.below == FillStatus.Undefined)
                                toggle = evSegment.closed;
                            else
                                toggle = evSegment.above != evSegment.below;

                            // merge two segments that belong to the same polygon
                            // think of this as sandwiching two segments together, where `eve.seg` is
                            // the bottom -- this will cause the above fill flag to toggle
                            if (toggle)
                                evSegment.above ^= evSegment.above; //XOR with itself to flip
                        }
                        else
                        {
                            // merge two segments that belong to different polygons
                            // each segment has distinct knowledge, so no special logic is needed
                            // note that this can only happen once per segment in this phase, because we
                            // are guaranteed that all self-intersections are gone
                            evSegment.otherAbove = evSegment.above;
                            evSegment.otherBelow = evSegment.below;
                        }
                        segments[ev.segmentID] = evSegment;
                        eventQueue.RemoveAt(eventQueue.IndexOf(ev.other));
                        eventQueue.RemoveAt(eventQueue.IndexOf(ev));
                    }

                    if (!eventQueue[^1].Equals(ev)) //eventQueue[0] when sorted ascending (most left event is at index 0)
                        continue; // something was inserted before us in the event queue, so loop back around and process it before continuing                    

                    // insert event in status. Will be removed from status once we encounter ev.other 
                    statusQueue.InsertRange(eventIndexInStatus, 1);
                    statusQueue[eventIndexInStatus] = ev;
                }
                else
                {
                    // find the position of the start event
                    var i = statusQueue.IndexOf(ev.other);

                    // removing the start event from the status will create two new adjacent edges, so we'll needto check for those
                    if (i > 0 && i < statusQueue.Length - 1)
                        CheckIntersection(statusQueue[i - 1], statusQueue[i + 1]);

                    // remove the start event from the status
                    statusQueue.RemoveAt(i);

                    // if we've reached this point, we've calculated everything there is to
                    // know, so save the segment for reporting
                    if (!ev.primary)
                    {
                        // make sure `seg.myFill` actually points to the primary polygon though
                        if (evSegment.otherAbove == FillStatus.Undefined || evSegment.otherBelow == FillStatus.Undefined)
                            throw new Exception("PolyBool: Unexpected state of otherFill (FillStatus.Undefined)");
                        (evSegment.above, evSegment.otherAbove) = (evSegment.otherAbove, evSegment.above);
                        (evSegment.below, evSegment.otherBelow) = (evSegment.otherBelow, evSegment.below);
                    }
                    segments[ev.segmentID] = evSegment;
                    result.Add(evSegment);
                }

                // remove the event and continue
                eventQueue.RemoveAt(eventQueue.Length -1); // RemoveAt(0) when sorted ascending (most left event is at index 0)
            }
            return result;
        }
    }
}