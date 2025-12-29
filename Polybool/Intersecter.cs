using System;
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
			prevSegmentIDs = new int4 ( -1, -1, -1, -1 );
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
					ref var evSegment = ref segments.ElementAt(ev.segmentID);
					bool keepAbove = false, keepBelow = false;
					if (hasAbove)
					{
						ref var aboveSegment = ref segments.ElementAt(above.segmentID);
						CheckIntersection(ev, ref evSegment, above, ref aboveSegment, out keepAbove);
					}
					if (!keepAbove && hasBelow)
					{
						ref var belowSegment = ref segments.ElementAt(below.segmentID);
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

					
					ref var evSegment = ref segments.ElementAt(ev.segmentID);
					

					// removing the start event from the status will create two new adjacent edges, so we'll need to check for those
					if (hasAbove && hasBelow)
					{
						ref var aboveSegment = ref segments.ElementAt(above.segmentID);
						ref var belowSegment = ref segments.ElementAt(below.segmentID);
						CheckIntersection(above, ref aboveSegment, below, ref belowSegment, out bool keepBelow);
						if (keepBelow)
						{							
							MergeColinearSegments(ref belowSegment, below.segmentID, ref evSegment, belowIndexInStatus);

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
			// returns the segment equal to ev1, or false if nothing equal
			long2 a0 = seg1.p0;
			long2 a1 = seg1.p1;
			long2 b0 = seg2.p0;
			long2 b1 = seg2.p1;
			keepEv2 = false;

			var intersectResult = Segment.SegmentLineIntersectSegmentLine(a0, a1, b0, b1, false, out double tA1, out double tB1, out double tA2, out double tB2);

			if (intersectResult == IntersectionResultType.Nothing)
				return;
			else if (intersectResult == IntersectionResultType.One)
			{
				// process a single intersection

				long2 ip;
				if (tB1 == 0) ip = b0;
				else if (tB1 == 1) ip = b1;
				else if (tA1 == 0) ip = a0;
				else if (tA1 == 1) ip = a1;
				else
				{
					// rounding x up is crucial as events are ordered by x and we need to ensure the generated left segment can still intersect
					// with other segment that are at the same x coordinate still in the event queue if we round down, the left event is
					// smaller than the current event queue head (=we would go into the past) and we would miss intersections!
					ip.x = (long)math.ceil(a0.x + tA1 * (a1.x - a0.x)); //test 62 will fail with ceil (passes with floor)
					ip.y = (long)math.floor(a0.y + tA1 * (a1.y - a0.y)); //strangely, more clipper unit tests work when rounding y down, e.g. polygon 140..but some fail
				}

				// is A divided between its endpoints? Exclude endpoints to avoid creation of zero length segments. Need to verify this explicity due to rounding errors
				if (tA1 > 0 && tA1 < 1 && !(seg1.p0 == ip || seg1.p1 == ip))
					DivideEvent(ev1, ref seg1, ip); //tA1

				// is B divided between its endpoints? Exclude endpoints to avoid creation of zero length segments. Need to verify this explicity due to rounding errors
				if (tB1 > 0 && tB1 < 1 && !(seg2.p0 == ip || seg2.p1 == ip))
					DivideEvent(ev2, ref seg2, ip);

				return;
			}
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
						DivideEvent(ev2, ref seg2, a1); //tB2
					}
					else
					{
						//  (a0)----------(a1)
						//  (b0)---(b1)
						DivideEvent(ev1, ref seg1, b1);//tA2
					}
					keepEv2 = true;
					return;
				}
				else if (tB1 > 0 && tB1 < 1)
					if (tA2 == 1 && tB2 == 1)
					{
						//         (a0)---(a1)
						//  (b0)----------(b1)
						DivideEvent(ev2, ref seg2, a0); //tB1,
					}
					else
					{
						// make a1 equal to b1
						if (tA2 == 1)
						{
							//         (a0)---(a1)
							//  (b0)-----------------(b1)
							DivideEvent(ev2, ref seg2, a1); //tB2,
						}
						else
						{
							//         (a0)----------(a1)
							//  (b0)----------(b1)
							DivideEvent(ev1, ref seg1, b1); //tA2,
						}
						//         (a0)---(a1)
						//  (b0)----------(b1)
						DivideEvent(ev2, ref seg2, a0); // tB1,
					}
				return;
			}

			Debug.Log("PolyBool: Unknown intersection type");
			return;
		}
		public void AddRegion(Polygon region, int start, int end, bool isPrimary)
		{
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

				var segNew = forward < 0 ? new Segment(from, to, isPrimary, closedPath) : new Segment(to, from, isPrimary, closedPath);
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
		void CreateEvents(int segmentID, out EventBool evStart, out EventBool evEnd)
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
		void DivideEvent(EventBool ev, ref Segment evSegment, long2 ip)
		{
			// slides an end backwards
			//   (start)------------(end)    to:
			//   (start)---(end)
			var forward = evSegment.p0.CompareTo(ip);
			evSegment.Split(ip, out Segment right);
			if (forward > 0)
			{
				(evSegment.p0, evSegment.p1) = (evSegment.p1, evSegment.p0);
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
			forward = right.p0.CompareTo(right.p1);
			if (forward > 0)
				(right.p0, right.p1) = (right.p1, right.p0);

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
		void AddSegmentToResult(int eventSegmentID,  ref Segment eventSegment, int belowIndexInStatus)
		{
			//overlapping vertical segments (some created due to rounding errors in Divide Events) are often are identical with the last added segments.
			//Emperically it's in 99% of the cases the last segment included in the results, but can be up to last 6. So check the last 6 segments
			//To-Do: how to prevent adding segments to results that end up being identical to future segments? This should not happen
			bool updatedPreviousSegment = false;
			for (int i = 3; i >= 0; i--)
			{
				if (prevSegmentIDs[i] != -1)
				{
					var prevSegment = segments[prevSegmentIDs[i]];
					if (prevSegment.p0 == eventSegment.p0 && prevSegment.p1 == eventSegment.p1)
					{
						if (!prevSegment.isPrimary)
						{
							FlipAnnotation(ref prevSegment); //unflip annotation prior to MergeColinearSegments to mimic normal annotation loop behaviour
							MergeColinearSegments(ref prevSegment, prevSegmentIDs[i], ref eventSegment, belowIndexInStatus);
							FlipAnnotation(ref prevSegment); //flip it again to  make sure `seg.myFill` actually points to the primary polygon
						}
						else
							MergeColinearSegments(ref prevSegment, prevSegmentIDs[i], ref eventSegment, belowIndexInStatus);
						updatedPreviousSegment = true;
						eventSegment.inResults = false;
						segments[prevSegmentIDs[i]] = prevSegment;
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

			//remember last 5 added segments
			for (int i = 0; i < 3; i++)
				prevSegmentIDs[i] = prevSegmentIDs[i + 1];
			prevSegmentIDs[3] = eventSegmentID;

		}

	}
}