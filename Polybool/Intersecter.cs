using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        public NativeList<Segment> segments;
        FillRule fillRule;
        int4 prevSegmentIDs;
        public Intersecter(bool selfIntersection, int size, FillRule fillRule, Allocator allocator)
        {
            this.selfIntersection = selfIntersection;
            this.fillRule = fillRule;
            eventQueue = new NativeList<EventBool>(2 * size, allocator);
            statusQueue = new NativeList<EventBool>(64, allocator);
            segments = new NativeList<Segment>(size, allocator);
            prevSegmentIDs = new int4(-1,-1,-1,-1);
        }
        public void Reset(bool selfIntersection)
        {
            this.selfIntersection = selfIntersection;
            eventQueue.Clear();
            statusQueue.Clear();
            segments.Clear();
            for (int i = 0; i < 4; i++)
                prevSegmentIDs[i] = -1;
        }

        public void Calculate(bool primaryPolyInverted, bool secondaryPolyInverted)
        {
            EventBool above = EventBool.Empty, below = EventBool.Empty;
            while (eventQueue.Length > 0)
            {
                var ev = eventQueue[^1]; //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                if (ev.isStart)
                {   
                    var belowIndexInStatus = StatusQueueComparerClass.BinarySearch(statusQueue, ev, segments);
                    belowIndexInStatus = belowIndexInStatus < 0 ? ~belowIndexInStatus : belowIndexInStatus;
                    bool hasAbove = belowIndexInStatus > 0;
                    bool hasBelow = belowIndexInStatus < statusQueue.Length;
                    above = hasAbove ? statusQueue[belowIndexInStatus - 1] : EventBool.Empty;
                    below = hasBelow ? statusQueue[belowIndexInStatus] : EventBool.Empty;

                    //check for intersections between new event and events in status
                    var evSegment = segments[ev.segmentID]; //do not fetch via ref because CheckIntersection will add segments, which invalidates the ref
                    bool keepAbove = false, keepBelow = false;
                    if (hasAbove)
                    {
                        var aboveSegment = segments[above.segmentID];
                        CheckIntersection(ev, ref evSegment, above, ref aboveSegment, out keepAbove);
                    }
                    if (!keepAbove && hasBelow)
                    {
                        var belowSegment = segments[below.segmentID];
                        CheckIntersection(ev, ref evSegment, below, ref belowSegment, out keepBelow); //expensive: ~30% of function execution time, (all which in DivideEvent -> eventQueue.IndexOf(ev.other)))
                    }

                    
                    if (keepAbove || keepBelow)
                    {                        
                        if (keepAbove)
                        {
                            ref var aboveSegment = ref segments.ElementAt(above.segmentID);
                            MergeColinearSegments(ref aboveSegment, above.segmentID, ref evSegment, belowIndexInStatus);
                        }
                        if (keepBelow)
                        {
                            ref var belowSegment = ref segments.ElementAt(below.segmentID);
                            MergeColinearSegments(ref belowSegment, below.segmentID, ref evSegment, belowIndexInStatus);
                        }

                        int eventIndexInEvents = eventQueue.Length - 1; //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                        if (eventQueue[eventIndexInEvents] != ev)
                            eventIndexInEvents = eventQueue.IndexOf(ev);   //event is not guaranteed to be at queue head because CheckIntersection can inserted new events prior to it
                        eventQueue.RemoveAt(eventIndexInEvents);
                        eventQueue.RemoveAt(eventQueue.IndexOf(ev.other)); //expensive: ~30% of function execution time
                    }

                    if (eventQueue[^1] != ev) //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                        continue; // something was inserted before us in the event queue, so loop back around and process it before continuing

                    // calculate fill flags
                    CalculateFill(ref evSegment, belowIndexInStatus, hasBelow, below);
                    segments[ev.segmentID] = evSegment;

                    // insert event in status. Will be removed from status once we encounter ev.other 
                    statusQueue.InsertRange(belowIndexInStatus, 1);
                    statusQueue[belowIndexInStatus] = ev;

                }
                else
                {
                    // find the position of the start event
                    var evOther = ev.other;
                    var evOtherInStatus = statusQueue.IndexOf(evOther);
                    var belowIndexInStatus = evOtherInStatus + 1;
                    bool hasAbove = evOtherInStatus > 0;
                    bool hasBelow = evOtherInStatus < statusQueue.Length - 1; //because evOtherInStatus is already in status, it could be the last element
                    above = hasAbove ? statusQueue[evOtherInStatus - 1] : EventBool.Empty;
                    below = hasBelow ? statusQueue[evOtherInStatus + 1] : EventBool.Empty;
                    
                    var evSegment = segments[ev.segmentID];  //do not fetch via ref because CheckIntersection will add segments, which invalidates the ref                  

                    // removing the start event from the status will create two new adjacent edges, so we'll need to check for those
                    if (hasAbove && hasBelow)
                    {
                        var aboveSegment = segments[above.segmentID]; //do not fetch via ref because CheckIntersection will add segments, which invalidates the ref
                        var belowSegment = segments[below.segmentID]; //do not fetch via ref because CheckIntersection will add segments, which invalidates the ref
                        CheckIntersection(above, ref aboveSegment, below, ref belowSegment, out bool keepBelow);
                        if (keepBelow)
                        {                            
                            MergeColinearSegments(ref belowSegment, below.segmentID, ref evSegment, belowIndexInStatus);
                            segments[below.segmentID] = belowSegment;

                            //now discard ev from events (ev.other is long gone) and ev + ev.other from status queue
                            int eventIndexInEvents = eventQueue.Length - 1; //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                            if (eventQueue[eventIndexInEvents] != ev)
                                eventIndexInEvents = eventQueue.IndexOf(ev);   //event is not guaranteed to be at queue head because CheckIntersection can inserted new events prior to it
                            eventQueue.RemoveAt(eventIndexInEvents);
                            statusQueue.RemoveAt(evOtherInStatus); //ev was just discarded, so remove also from status
                            continue;
                        }
                        else if (eventQueue[^1] != ev) //eventQueue[0] when sorted ascending (most left event is at index 0), otherwise eventQueue[^1]
                            continue; // something was inserted before us in the event queue, so loop back around and process it before continuing
                    }

                    // remove the start event from the status
                    statusQueue.RemoveAt(evOtherInStatus);

                    // if we've reached this point, we've calculated everything there is to
                    // know, so save the segment for reporting
                    AddSegmentToResult(ev.segmentID, ref evSegment, belowIndexInStatus);
                }
                // remove the event and continue
                eventQueue.RemoveAt(eventQueue.Length - 1); // eventQueue.RemoveAt(0) when sorted ascending (most left event is at index 0), otherwise eventQueue.RemoveAt(eventQueue.Length - 1)
            }
        }

        void CheckIntersection(EventBool ev1, ref Segment seg1, EventBool ev2, ref Segment seg2, out bool keepEv2)
        { 
            keepEv2 = false; // indicate if ev2 is equal to ev1, in which case we keep it
            var intersectResult = Segment.SegmentLineIntersectSegmentLine(ref seg1, ref seg2, out Rational tA1, out Rational tB1, out Rational tA2, out Rational tB2);

            if (intersectResult == IntersectionResultType.Nothing)
                return;
            else if (intersectResult == IntersectionResultType.One)
            {
                // process a single intersection
                // is A divided between its endpoints? Ensure endpoints are excluded via InRangeStrict to avoid creation of zero length segments
                if (tA1.InRangeStrict())
                    DivideEvent(ev1, ref seg1, tA1); //tA1

                // is B divided between its endpoints? Exclude endpoints to avoid creation of zero length segments. Need to verify this explicity due to rounding errors
                if (tB1.InRangeStrict())
                    DivideEvent(ev2, ref seg2, tB1);

                return;
            }
            else if (intersectResult == IntersectionResultType.Two)
            {
                // segments are parallel or coincident
                if((tA1.IsOne() && tA2.IsOne() && tB1.IsZero() && tB2.IsZero()) ||
                   (tA1.IsZero() && tA2.IsZero() && tB1.IsOne() && tB2.IsOne()) )
                    return; // segments touch at endpoints... no intersection

                if (tA1.IsZero() && tA2.IsOne() && tB1.IsZero() && tB2.IsOne())
                {
                    keepEv2 = true; // segments are exactly equal
                    return;
                }

                if (tA1.IsZero() && tB1.IsZero())
                {
                    if (tA2.IsOne())
                    {
                        //  (a0)---(a1)
                        //  (b0)----------(b1)
                        DivideEvent(ev2, ref seg2, tB2);
                    }
                    else
                    {
                        //  (a0)----------(a1)
                        //  (b0)---(b1)
                        DivideEvent(ev1, ref seg1, tA2);
                    }
                    keepEv2 = true;
                    return;
                }
                else if (tB1.InRangeStrict())
                    if (tA2.IsOne() && tB2.IsOne())
                    {
                        //         (a0)---(a1)
                        //  (b0)----------(b1)
                        DivideEvent(ev2, ref seg2, tB1);
                    }
                    else
                    {
                        // make a1 equal to b1
                        if (tA2.IsOne())
                        {
                            //         (a0)---(a1)
                            //  (b0)-----------------(b1)
                            DivideEvent(ev2, ref seg2, tB2);
                        }
                        else
                        {
                            //         (a0)----------(a1)
                            //  (b0)----------(b1)
                            DivideEvent(ev1, ref seg1, tA2);
                        }
                        //         (a0)---(a1)
                        //  (b0)----------(b1)
                        DivideEvent(ev2, ref seg2, tB1);
                    }
                return;
            }

            Debug.Log("PolyBool: Unknown intersection type");
            return;
        }
        public void AddRegion(Polygon region, int start, int end, bool isPrimary)
        {
            var startParamPoint = new Rational(0, 1);
            var endParamPoint = new Rational(1, 1);
            var nodes = region.nodes;
            long2 from;
            long2 to = nodes[end - 1];
            //determine if path is closed or not. bolean opperations will fail if this is not correctly set
            //review if there is a better was to determine or permit user to define if path is closed or open
            var closedPath = nodes[end - 1] == nodes[start];
            for (int i = start; i < end; i++)
            {
                from = to;
                to = nodes[i];

                int forward = from.CompareTo(to);
                if (forward == 0)
                    continue; // points are equal, so we have a zero-length segment

                var segNew = forward < 0 ? new Segment(from, to, startParamPoint, endParamPoint, isPrimary, closedPath) : new Segment(to, from, startParamPoint, endParamPoint, isPrimary, closedPath);
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
                seg.isPrimary = true;
                seg.inResults = false;
                this.segments.Add(seg);
                CreateEvents(segmentID, out EventBool evStart, out EventBool evEnd);
                AddEvent(evStart);
                AddEvent(evEnd);
            }
            for (int i = 0, end = segments2.Length; i < end; i++)
            {
                var segmentID = this.segments.Length;
                var seg = segments2[i];
                seg.isPrimary = false;
                seg.inResults = false;
                this.segments.Add(seg);
                CreateEvents(segmentID, out EventBool evStart, out EventBool evEnd);
                AddEvent(evStart);
                AddEvent(evEnd);
            }
        }
        #region event handling
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CreateEvents(int segmentID, out EventBool evStart, out EventBool evEnd)
        {
            evStart = new EventBool(true, segmentID);
            evEnd = new EventBool(false, segmentID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddEvent(EventBool ev)
        {
            var eventIndexInEvents = EventQueueComparerClass.BinarySearch(eventQueue, ev, segments, -1); // -1 = sort events descending for 30% speedup
            eventIndexInEvents = eventIndexInEvents < 0 ? ~eventIndexInEvents : eventIndexInEvents;
            eventQueue.InsertRange(eventIndexInEvents, 1);
            eventQueue[eventIndexInEvents] = ev;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DivideEvent(EventBool ev, ref Segment evSegment, Rational ip)
        {
            int leftForward, rightforward;
            if ((leftForward = evSegment.start.CompareTo(ip)) == 0 || (rightforward = ip.CompareTo(evSegment.end)) == 0)
                return;// we would create a zero length segment

            // slides an end backwards
            //   (start)------------(end)    to:
            //   (start)---(end)             

            evSegment.Split(ip, out Segment right);
            if (leftForward > 0)
            {
                Debug.Log($"Segment direction inversion detected...likely an integer overflow when calculating nom/denom, change to 128 bit multiplication?");
                (evSegment.start, evSegment.end) = (evSegment.end, evSegment.start);
                var eventIndexInEvents = eventQueue.IndexOf(ev);
                if (eventIndexInEvents != -1) //unless we split the current event queue head, the start event is not in the event queue anymore!
                {
                    eventQueue.RemoveAt(eventIndexInEvents);
                    AddEvent(ev);
                }
            }
            segments[ev.segmentID] = evSegment;

            //remove and add the "other" event of the left segment, because new endpoint will cause new position in event queue
            var evOther = ev.other;
            var evOtherInEvents = eventQueue.IndexOf(ev.other);
            eventQueue.RemoveAt(evOtherInEvents);
            AddEvent(evOther);

            //create and add the right segment events to event queue
            if (rightforward > 0)
            {
                Debug.Log($"Segment direction inversion detected...likely an integer overflow, change to 128 bit multiplication?");
                (right.start, right.end) = (right.end, right.start);
            }

            var rightSegmentID = segments.Length;
            segments.Add(right);
            CreateEvents(rightSegmentID, out EventBool evStart, out EventBool evEnd);
            AddEvent(evStart);
            AddEvent(evEnd);
        }
        #endregion event handling     

        #region fill annotation
        void CalculateFill(ref Segment evSegment, int belowIndexInStatus, bool hasBelow, EventBool below)
        {
            if (selfIntersection) // set my fill 
            {
                if (fillRule == FillRule.EvenOdd)
                    GetEvenOddAnnotation(ref evSegment, hasBelow, below);
                else //Non-Zero, Positive, Negative
                    GetWindingForSegment(ref evSegment, belowIndexInStatus);
            }
            else // combination phase: set other fill 
            {                   
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
                        var belowSegment = segments[below.segmentID];
                        if (evSegment.isPrimary == belowSegment.isPrimary)
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
        void GetEvenOddAnnotation(ref Segment evSegment, bool hasBelow, EventBool below)
        {
            // FillRule.EvenOdd
            bool toggle;
            // (1) determine if the edge is a "toggling edge"
            if (!evSegment.myFillSet)
            {
                toggle = evSegment.closed;                                  // new segment. toggle if we're part of a closed path
                evSegment.myFillSet = true;
            }
            else
                toggle = evSegment.fillAbove != evSegment.fillBelow;        // segment resulted from division, and is toggling when above and below fill are not the same

            // (2) determine below fill
            if (!hasBelow)
                evSegment.fillBelow = false;                                // no segment is below us, so not filled
            else
                evSegment.fillBelow = segments[below.segmentID].fillAbove;  // copy the above fill from the segment below 

            // (3) determine above fill
            if (toggle)
                evSegment.fillAbove = !evSegment.fillBelow;                 //above fill is opposite of below fill
            else
                evSegment.fillAbove = evSegment.fillBelow;                  //above fill is same as below fill
        }
        void GetWindingForSegment(ref Segment segment, int belowIndexInStatus)
        {
            //when determining fill status from winding, we do not care about prior annotation of segment that resulted from splits,
            //because there could be many more segments below this right segment in the status by now, so just establish the current winding at the status
            segment.myFillSet = true;

            // (1) determine winding below current event segment by summing all windings from eventIndexInStatus towards bottom of status
            int windingBelow = 0, windingAbove;
            for (int i = belowIndexInStatus, end = statusQueue.Length; i < end; i++)
                windingBelow += segments[statusQueue[i].segmentID].windingTopToBottom;

            // (2) determine winding above current event segment. Simply add "winding" from event segment.
            // For a vertical edge, the winding does NOT change along y axis,
            // but it does change along x-axis, so use "windingLeftToRight" value instead
            windingAbove = segment.windingTopToBottom == 0 ? windingBelow + segment.windingLeftToRight : windingBelow + segment.windingTopToBottom;

            // (3) derive fill annotation from winding for FillRule.NonZero, FillRule.Positive, FillRule.Negative
            // NonZero: winding !=0 means "inside/filled" and winding = 0 means "outside" "not filled"
            // Positive: winding >0 means "inside/filled", otherwise it means "outside" "not filled"
            // Negative: winding <0 means "inside/filled", otherwise it means "outside" "not filled"
            switch (fillRule)
            {
                case FillRule.NonZero:
                    segment.fillBelow = windingBelow != 0;
                    segment.fillAbove = windingAbove != 0;
                    break;
                case FillRule.Positive:
                    segment.fillBelow = windingBelow > 0;
                    segment.fillAbove = windingAbove > 0;
                    break;
                case FillRule.Negative:
                    segment.fillBelow = windingBelow < 0;
                    segment.fillAbove = windingAbove < 0;
                    break;
            }
        }
        /// <summary>Merge eventSegment fill information into priorEventSegment fill </summary>
        void MergeColinearSegments(ref Segment priorEventSegment, int priorEventSegmentID, ref Segment eventSegment, int belowIndexInStatus)
        {
            // ev and eve are equal
            // we'll keep eve and throw away ev
            if (!selfIntersection)
            {
                // merge two segments that belong to different polygons
                // each segment has distinct knowledge, so no special logic is needed
                // note that this can only happen once per segment in this phase, because we
                // are guaranteed that all self-intersections are gone
                priorEventSegment.fillOtherAbove = eventSegment.fillAbove;
                priorEventSegment.fillOtherBelow = eventSegment.fillBelow;
            }
            else
            {
                if (fillRule == FillRule.EvenOdd)
                {
                    bool toggle; // are we a toggling edge?
                    if (!eventSegment.myFillSet)
                        toggle = eventSegment.closed;
                    else
                        toggle = eventSegment.fillAbove != eventSegment.fillBelow;

                    // merge two segments that belong to the same polygon
                    // think of this as sandwiching two segments together, where `eve.seg` is
                    // the bottom -- this will cause the above fill flag to toggle							
                    if (toggle)
                        priorEventSegment.fillAbove = !priorEventSegment.fillAbove;
                }
                else
                {
                    // FillRule.NonZero, FillRule.Positive, FillRule.Negative: derive fill annotation from winding
                    // NonZero: winding !=0 means "inside/filled" and winding = 0 means "outside" "not filled"
                    // Positive: winding >0 means "inside/filled", otherwise it means "outside" "not filled"
                    // Negative: winding <0 means "inside/filled", otherwise it means "outside" "not filled"

                    // (1) add the windings of the 2 colinera segments
                    // if they go in opposite direction they cancel eachother out
                    // otherwise they add up..so clamp back to -1 .. 1
                    var mergedWindingLeftToRight = priorEventSegment.windingLeftToRight + eventSegment.windingLeftToRight;
                    var mergedWindingTopToBottom = priorEventSegment.windingTopToBottom + eventSegment.windingTopToBottom;
                    mergedWindingLeftToRight = Math.Clamp(mergedWindingLeftToRight, -1, 1);
                    mergedWindingTopToBottom = Math.Clamp(mergedWindingTopToBottom, -1, 1);
                    priorEventSegment.windingLeftToRight = eventSegment.windingLeftToRight = mergedWindingLeftToRight;
                    priorEventSegment.windingTopToBottom = eventSegment.windingTopToBottom = mergedWindingTopToBottom;

                    segments[priorEventSegmentID] = priorEventSegment;//we need to store the changed segment winding prior adding all the windings of the status segments
                    GetWindingForSegment(ref priorEventSegment, belowIndexInStatus);
                }               
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FlipAnnotation(ref Segment segment)
        {
            if (!segment.otherFillSet) throw new Exception("PolyBool: Unexpected state of otherFill (FillStatus.Undefined)");
            (segment.fillAbove, segment.fillOtherAbove) = (segment.fillOtherAbove, segment.fillAbove);
            (segment.fillBelow, segment.fillOtherBelow) = (segment.fillOtherBelow, segment.fillBelow);
        }
        #endregion fill annotation
        void AddSegmentToResult(int eventSegmentID, ref Segment eventSegment, int belowIndexInStatus)
        {
            // sometimes a segment added to the result was just added 1 or 2 event queue loops before. This is a consequence of intersections creating new segments with changes start and end coordinates
            // Using the parametric approach to storing segments and sorting the events in the event queue vastly reduces this, but it still happens rarely.
            // Catch and fix this here to avoid ending up with open chains in segment chainer
            bool updatedPreviousSegment = false;
            for (int i = 3; i >= 0; i--)
            {
                var prevSegmentID = prevSegmentIDs[i];
                if (prevSegmentIDs[i] != -1)
                {                    
                    var prevSegment = segments[prevSegmentID];
                    int compStart = Segment.Compare(prevSegment.p0, prevSegment.dxy, prevSegment.start, eventSegment.p0, eventSegment.dxy, eventSegment.start);
                    int compEnd = Segment.Compare(prevSegment.p0, prevSegment.dxy, prevSegment.end, eventSegment.p0, eventSegment.dxy, eventSegment.end);
                    if (compStart==0 && compEnd==0)
                    {
                        if (!prevSegment.isPrimary)
                        {
                            FlipAnnotation(ref prevSegment); //unflip annotation prior to MergeColinearSegments to mimic normal annotation loop behaviour
                            MergeColinearSegments(ref prevSegment, prevSegmentID, ref eventSegment, belowIndexInStatus);
                            FlipAnnotation(ref prevSegment); //flip it again to  make sure `seg.myFill` actually points to the primary polygon
                        }
                        else
                            MergeColinearSegments(ref prevSegment, prevSegmentID, ref eventSegment, belowIndexInStatus);
                        updatedPreviousSegment = true;
                        eventSegment.inResults = false;
                        segments[prevSegmentID] = prevSegment;
                        break;
                    }
                }
            }
            if (!updatedPreviousSegment)
            {
                eventSegment.inResults = true;
                updatedPreviousSegment = false;
            }

            if (!eventSegment.isPrimary)
                FlipAnnotation(ref eventSegment);                      // make sure `seg.myFill` actually points to the primary polygon though
            segments[eventSegmentID] = eventSegment;

            //remember last 3 added segments
            for (int i = 0; i < 3; i++)
                prevSegmentIDs[i] = prevSegmentIDs[i + 1];
            prevSegmentIDs[3] = eventSegmentID;
        }
    }
}