using System;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace TextMeshDOTS.Polybool
{
    public struct Intersecter
    {
        public bool selfIntersection;
        NativeList<EventBool> eventQueue;
        NativeList<EventBool> statusQueue;
        public NativeList<Segment> segments;
        FillRule fillRule;
        FixedList32Bytes<int> prevSegmentIDs;
        public Intersecter(bool selfIntersection, int size, FillRule fillRule, Allocator allocator)
        {
            this.selfIntersection = selfIntersection;
            this.fillRule = fillRule;
            eventQueue = new NativeList<EventBool>(2 * size, allocator);
            statusQueue = new NativeList<EventBool>(64, allocator);
            segments = new NativeList<Segment>(size, allocator);
            prevSegmentIDs = new FixedList32Bytes<int> {-1, -1, -1, -1};
        }
        public void Reset(bool selfIntersection)
        {
            this.selfIntersection = selfIntersection;
            eventQueue.Clear();
            statusQueue.Clear();
            segments.Clear();
            for (int i = 0; i < 3; i++)
                prevSegmentIDs[i] = -1;
        }
        #region initialize events
        public void AddRegion(Polygon region, int start, int end, SegmentType segmentType)
        {
            long2 from;
            long2 to = region.nodes[end - 1];
            for (int i = start; i < end; i++)
            {
                from = to;
                to = region.nodes[i];

                int forward = from.CompareTo(to);
                if (forward == 0)
                    continue; // points are equal, so we have a zero-length segment

                var segNew = forward < 0 ? new Segment(from, to, segmentType, true) : new Segment(to, from, segmentType, true);
                if (fillRule == FillRule.NonZero)
                {
                    segNew.windingTopToBottom = PointUtils.GetWindingTowardsBottom(from, to);
                    segNew.windingLeftToRight = PointUtils.GetWindingTowardsRight(from, to);
                }

                var segmentID = segments.Length;
                segments.Add(segNew);
                CreateEvents(segmentID, out EventBool evStart, out EventBool evEnd);
                AddEvent(evStart);
                AddEvent(evEnd);
            }
        }        
        public void AddSegments(NativeList<Segment> segments1, NativeList<Segment> segments2)
        {

            for (int i = 0, end = segments1.Length; i < end; i++)
            {
                var segmentID = this.segments.Length;
                var seg = segments1[i];
                seg.segmentType = SegmentType.Primary;
                this.segments.Add(seg);
                CreateEvents(segmentID, out EventBool evStart, out EventBool evEnd);
                AddEvent(evStart);
                AddEvent(evEnd);
            }
            for (int i = 0, end = segments2.Length; i < end; i++)
            {
                var segmentID = this.segments.Length;
                var seg = segments2[i];
                seg.segmentType = SegmentType.Secondary;
                this.segments.Add(seg);
                CreateEvents(segmentID, out EventBool evStart, out EventBool evEnd);
                AddEvent(evStart);
                AddEvent(evEnd);
            }
        }
        #endregion

        void CreateEvents(int segmentID, out EventBool evStart, out EventBool evEnd)
        {
            evStart = new EventBool(true, segmentID);
            evEnd = new EventBool(false, segmentID);
        }

        void DivideEvent(EventBool ev, double t, long2 p)
        {
            var rightSegmentID = segments.Length;
            ref var seg = ref segments.ElementAt(ev.segmentID);
            var forward = seg.start.CompareTo(p);
            if (forward == 0 || seg.end.CompareTo(p) == 0)
                return; // points are equal, so we have a zero-length segment

            seg.Split(p, out Segment right);
            if (forward > 0)
            {
                (seg.start, seg.end) = (seg.end, seg.start);
                int eventIndexInEvents = eventQueue.Length - 1; //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                if (Hint.Unlikely(eventQueue[eventIndexInEvents] != ev))
                    eventIndexInEvents = eventQueue.IndexOf(ev);   //event is not guaranteed to be at queue head because CheckIntersection can inserted new events prior to it
                eventQueue.RemoveAt(eventIndexInEvents);
                AddEvent(ev);
            }

            // slides an end backwards
            //   (start)------------(end)    to:
            //   (start)---(end)
            //remove and add the "other" event of the left segment, because new endpoint will cause new position in event queue
            var evOther = ev.other;
            var evOtherInEvents = eventQueue.IndexOf(ev.other);
            eventQueue.RemoveAt(evOtherInEvents);
            AddEvent(evOther);


            //create and add the right segment events to event queue
            forward = right.start.CompareTo(right.end);
            if (forward > 0)
                (right.start, right.end) = (right.end, right.start);

            segments.Add(right);
            CreateEvents(rightSegmentID, out EventBool evStart, out EventBool evEnd);
            AddEvent(evStart);
            AddEvent(evEnd);
        }
        void AddEvent(EventBool ev)
        {
            var eventIndexInEvents = EventQueueComparerClass.BinarySearch(eventQueue, ev, segments, -1); // -1 = sort events descending for 30% speedup
            eventIndexInEvents = eventIndexInEvents < 0 ? ~eventIndexInEvents : eventIndexInEvents;
            eventQueue.InsertRange(eventIndexInEvents, 1);
            eventQueue[eventIndexInEvents] = ev;
        }

        void CheckIntersection(EventBool ev1, EventBool ev2, out bool keepEv2)
        {
            // returns the segment equal to ev1, or false if nothing equal
            ref var seg1 = ref segments.ElementAt(ev1.segmentID);
            ref var seg2 = ref segments.ElementAt(ev2.segmentID);
            long2 a0 = seg1.start;
            long2 a1 = seg1.end;
            long2 b0 = seg2.start;
            long2 b1 = seg2.end;
            keepEv2 = false;

            var intersectResult = Segment.SegmentLineIntersectSegmentLine(a0, a1, b0, b1, false, out double tA1, out double tB1, out double tA2, out double tB2);

            if (intersectResult == IntersectionResultType.Nothing)
                // no intersections
                return;
            else if (intersectResult == IntersectionResultType.Two)
            {
                // segments are parallel or coincident

                if (tA1 == 1 && tA2 == 1 && tB1 == 0 && tB2 == 0 ||
                    tA1 == 0 && tA2 == 0 && tB1 == 1 && tB2 == 1)
                    return; // segments touch at endpoints... no intersection

                if (tA1 == 0 && tA2 == 1 && tB1 == 0 && tB2 == 1)
                {
                    keepEv2 = true; // segments are exactly equal
                    return;
                }

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
                    keepEv2 = true;
                    return;
                }
                else if (tB1 > 0 && tB1 < 1)
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
                return;
            }
            else if (intersectResult == IntersectionResultType.One)
            {
                // process a single intersection

                // even though *in theory* seg1.data.point(tA) === seg2.data.point(tB), that isn't exactly
                // correct in practice because intersections aren't exact... so we need to calculate a single
                // intersection point that everyone can share
                var p = tB1 == 0 ? seg2.start :
                        tB1 == 1 ? seg2.end :
                        tA1 == 0 ? seg1.start :
                        tA1 == 1 ? seg1.end :
                        long2.Lerp(seg1.start, seg1.end, tA1);

                // is A divided between its endpoints? (exclusive)
                if (tA1 > 0 && tA1 < 1)
                    DivideEvent(ev1, tA1, p);

                // is B divided between its endpoints? (exclusive)
                if (tB1 > 0 && tB1 < 1)
                    DivideEvent(ev2, tB1, p);

                return;
            }
            Debug.Log("PolyBool: Unknown intersection type");
            return;
        }
        void CalculateFill(EventBool ev, int eventIndexInStatus, bool hasBelow, EventBool above, EventBool below)
        {
            ref var evSegment = ref segments.ElementAt(ev.segmentID); //fetch evSegment again as an intersections will change the endpoint
            if (selfIntersection)
            {
                if (fillRule == FillRule.EvenOdd)
                {
                    // FillRule.EvenOdd
                    bool toggle;
                    // (1) determine if the edge is a "toggling edge"
                    if (!evSegment.myFillSet)
                    {
                        toggle = evSegment.closed;                             // new segment. toggle if we're part of a closed path
                        evSegment.myFillSet = true;
                    }
                    else
                        toggle = evSegment.fillAbove != evSegment.fillBelow;           // segment resulted from division, and is toggling when above and below fill are not the same

                    // (2) determine below fill
                    if (!hasBelow)
                        evSegment.fillBelow = false;                // no segment is below us, so not filled
                    else
                        evSegment.fillBelow = segments.ElementAt(below.segmentID).fillAbove;     // copy the above fill from the segment below 

                    // (3) determine above fill
                    if (toggle)
                        evSegment.fillAbove = !evSegment.fillBelow;//above fill is opposite of below fill
                    else
                        evSegment.fillAbove = evSegment.fillBelow;                        //above fill is same as below fill
                }
                else
                {
                    // FillRule.NonZero, FillRule.Positive, FillRule.Negative: derive fill annotation from winding
                    // NonZero: winding !=0 means "inside/filled" and winding = 0 means "outside" "not filled"
                    // Positive: winding >0 means "inside/filled", otherwise it means "outside" "not filled"
                    // Negative: winding <0 means "inside/filled", otherwise it means "outside" "not filled"

                    // (1) determine winding below current event segment by summing all windings from eventIndexInStatus towards bottom of status
                    int windingBelow = 0, windingAbove;
                    for (int i = eventIndexInStatus, end = statusQueue.Length; i < end; i++)
                        windingBelow += segments.ElementAt(statusQueue[i].segmentID).windingTopToBottom;

                    // (2) determine winding above current event segment. Simply add "winding" from event segment.
                    // For a vertical edge, the winding does NOT change along y axis,
                    // but it does change along x-axis, so use "windingLeftToRight" value instead
                    windingAbove = evSegment.windingTopToBottom == 0 ? windingBelow + evSegment.windingLeftToRight : windingBelow + evSegment.windingTopToBottom;

                    switch (fillRule)
                    {
                        case FillRule.NonZero:
                            evSegment.fillBelow = windingBelow != 0;
                            evSegment.fillAbove = windingAbove != 0;
                            break;
                        case FillRule.Positive:
                            evSegment.fillBelow = windingBelow > 0;
                            evSegment.fillAbove = windingAbove > 0;
                            break;
                        case FillRule.Negative:
                            evSegment.fillBelow = windingBelow < 0;
                            evSegment.fillAbove = windingAbove < 0;
                            break;
                    }
                }
            }
            else
            {
                // now we fill in any missing transition information, since we are all-knowing at this point                        
                if (!evSegment.otherFillSet)
                {
                    // if we don't have other information, then we need to figure out if we're inside the other polygon
                    bool inside;
                    if (!hasBelow)
                        // if nothing is below us, then we're not filled
                        inside = false;
                    else
                    {
                        // otherwise, something is below us
                        // so copy the below segment's other polygon's above
                        ref var belowSegment = ref segments.ElementAt(below.segmentID);
                        if (evSegment.segmentType == belowSegment.segmentType)
                        {
                            if (!belowSegment.otherFillSet)
                                throw new Exception("PolyBool: Unexpected state of otherFill (null)");
                            inside = belowSegment.fillOtherAbove;
                        }
                        else
                            inside = belowSegment.fillAbove;
                    }
                    evSegment.fillOtherAbove = inside;
                    evSegment.fillOtherBelow = inside;
                    evSegment.otherFillSet = true;

                }
            }
        }
        void MergeColinearSegments(ref Segment eveSegment, ref Segment evSegment)
        {
            // ev and eve are equal
            // we'll keep eve and throw away ev

            // merge ev.seg"s fill information into eve.seg
            if (selfIntersection)
            {
                //if (fillRule == FillRule.EvenOdd)
                {
                    bool toggle; // are we a toggling edge?
                    if (!evSegment.myFillSet)
                    { 
                        toggle = evSegment.closed;
                        evSegment.myFillSet = true;
                    }
                    else
                        toggle = evSegment.fillAbove != evSegment.fillBelow;

                    // merge two segments that belong to the same polygon
                    // think of this as sandwiching two segments together, where `eve.seg` is
                    // the bottom -- this will cause the above fill flag to toggle							
                    if (toggle)
                        eveSegment.fillAbove = !eveSegment.fillAbove;
                }
                //else
                //{
                //	//To-Do: figure out how to handle merging of colinear segments for FillRule.NonZero
                //}
            }
            else
            {
                // merge two segments that belong to different polygons
                // each segment has distinct knowledge, so no special logic is needed
                // note that this can only happen once per segment in this phase, because we
                // are guaranteed that all self-intersections are gone
                eveSegment.fillOtherAbove = evSegment.fillAbove;
                eveSegment.fillOtherBelow = evSegment.fillBelow;
            }
        }
        void AddOrOverwriteSegment(int segmentID)
        {
            ref var evSegment = ref segments.ElementAt(segmentID);
            if (evSegment.segmentType == SegmentType.Secondary)         // make sure `seg.myFill` actually points to the primary polygon though
            {				
                if (!evSegment.otherFillSet) throw new Exception("PolyBool: Unexpected state of otherFill (FillStatus.Undefined)");
                (evSegment.fillAbove, evSegment.fillOtherAbove) = (evSegment.fillOtherAbove, evSegment.fillAbove);
                (evSegment.fillBelow, evSegment.fillOtherBelow) = (evSegment.fillOtherBelow, evSegment.fillBelow);
            }
            
            //Microintersection lead to small segments that quite often are identical with the last 2 added segments. Check and update them
            for (int i = 2; i >= 0; i--)
            {
                if (prevSegmentIDs[i] != -1)
                {
                    ref var prevSegment = ref segments.ElementAt(prevSegmentIDs[i]);
                    if (prevSegment.start == evSegment.start && prevSegment.end == evSegment.end)
                    {
                        // previous identical segment found -->can we keep it and ignore the new segment?
                        if (prevSegment.fillAbove != evSegment.fillAbove || prevSegment.fillBelow != evSegment.fillBelow ||
                            prevSegment.fillOtherAbove != evSegment.fillOtherAbove || prevSegment.fillOtherBelow != evSegment.fillOtherBelow)
                            MergeColinearSegments(ref prevSegment, ref evSegment);//no, the segment differ in fill anotation. How to merge? There can only be one segment!						
                        break;
                    }
                }
            }
            evSegment.inResults = true;

            //remember last 3 added segments
            for (int i = 0; i < 2; i++)
                prevSegmentIDs[i] = prevSegmentIDs[i + 1];
            prevSegmentIDs[2] = segmentID;

        }
        public void Calculate(bool primaryPolyInverted, bool secondaryPolyInverted)
        {
            EventBool above = EventBool.Empty, below = EventBool.Empty;
            bool validateFillIndex = false;//when an DivideEvent inserts end event before the current event, then we need to revalidate all fill annotation up to this index
            while (eventQueue.Length > 0)
            {
                var ev = eventQueue[^1]; //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]                
                if (ev.isStart)
                {
                    validateFillIndex = false;
                    var eventIndexInStatus = StatusQueueComparerClass.BinarySearch(statusQueue, ev, segments);
                    eventIndexInStatus = eventIndexInStatus < 0 ? ~eventIndexInStatus : eventIndexInStatus;
                    bool hasAbove = eventIndexInStatus > 0;
                    bool hasBelow = eventIndexInStatus < statusQueue.Length;
                    above = hasAbove ? statusQueue[eventIndexInStatus - 1] : EventBool.Empty;
                    below = hasBelow ? statusQueue[eventIndexInStatus]: EventBool.Empty;

                    //check for intersections between new event and events in status
                    bool keepAbove = false, keepBelow = false;
                    if (hasAbove)
                        CheckIntersection(ev, above, out keepAbove);
                    if (!keepAbove && hasBelow)
                        CheckIntersection(ev, below, out keepBelow); //expensive: ~30% of function execution time, (all which in DivideEvent -> eventQueue.IndexOf(ev.other)))

                    if (keepAbove || keepBelow)
                    {
                        ref var evSegment = ref segments.ElementAt(ev.segmentID);
                        if (keepAbove)
                        {
                            ref var aboveSegment = ref segments.ElementAt(above.segmentID);
                            MergeColinearSegments(ref aboveSegment, ref evSegment);
                        }
                        if (keepBelow)
                        {
                            ref var belowSegment = ref segments.ElementAt(below.segmentID);
                            MergeColinearSegments(ref belowSegment, ref evSegment);
                        }
                        int eventIndexInEvents= eventQueue.Length - 1; //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                        if (Hint.Unlikely(eventQueue[eventIndexInEvents] != ev))
                            eventIndexInEvents = eventQueue.IndexOf(ev);   //event is not guaranteed to be at queue head because CheckIntersection can inserted new events prior to it
                        eventQueue.RemoveAt(eventIndexInEvents);
                        eventQueue.RemoveAt(eventQueue.IndexOf(ev.other)); //expensive: ~30% of function execution time
                    }

                    if (Hint.Unlikely(eventQueue[^1] != ev)) //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                    {
                        validateFillIndex = true;
                        continue; // something was inserted before us in the event queue, so loop back around and process it before continuing
                    }

                    // calculate fill flags
                    CalculateFill(ev, eventIndexInStatus, hasBelow, above, below);

                    // insert event in status. Will be removed from status once we enLengther ev.other 
                    statusQueue.InsertRange(eventIndexInStatus, 1);
                    statusQueue[eventIndexInStatus] = ev;
                }
                else
                {
                    // find the position of the start event
                    var evOtherInStatus = statusQueue.IndexOf(ev.other);
                    bool hasAbove = evOtherInStatus > 0;
                    bool hasBelow = evOtherInStatus < statusQueue.Length - 1; //because evOtherInStatus is already in status, it could be the last element
                    above = hasAbove ? statusQueue[evOtherInStatus - 1] : EventBool.Empty;
                    below = hasBelow ? statusQueue[evOtherInStatus + 1] : EventBool.Empty;
                    if (validateFillIndex)
                    {
                        CalculateFill(ev, evOtherInStatus, hasBelow, above, below);
                        validateFillIndex = false;
                    }

                    // removing the start event from the status will create two new adjacent edges, so we'll need to check for those
                    if (hasAbove && hasBelow)
                    {
                        CheckIntersection(above, below, out bool keepBelow);
                        if (keepBelow)
                        {
                            ref var evSegment = ref segments.ElementAt(ev.segmentID);
                            ref var belowSegment = ref segments.ElementAt(below.segmentID);
                            MergeColinearSegments(ref belowSegment, ref evSegment);
                            int eventIndexInEvents = eventQueue.Length -1; //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                            if (Hint.Unlikely(eventQueue[eventIndexInEvents] != ev))
                                eventIndexInEvents = eventQueue.IndexOf(ev);   //event is not guaranteed to be at queue head because CheckIntersection can inserted new events prior to it
                            eventQueue.RemoveAt(eventIndexInEvents);
                            var eventIndexInStatus = statusQueue.IndexOf(ev.other);//ev was just discarded, so remove also from status
                            statusQueue.RemoveAt(eventIndexInStatus);
                        }
                        if (Hint.Unlikely(eventQueue[^1] != ev)) //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                        {
                            validateFillIndex = true;
                            continue; // something was inserted before us in the event queue, so loop back around and process it before continuing
                        }
                    }

                    // remove the start event from the status
                    statusQueue.RemoveAt(evOtherInStatus);

                    // if we've reached this point, we've calculated everything there is to
                    // know, so save the segment for reporting
                    AddOrOverwriteSegment(ev.segmentID);
                }
                // remove the event and continue
                eventQueue.RemoveAt(eventQueue.Length - 1); // eventQueue.RemoveAt(0) when sorted ascending (most left event is at index 0), otherwise eventQueue.RemoveAt(eventQueue.Length - 1)
            }
        }
    }
}