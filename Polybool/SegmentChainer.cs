using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{
    public partial struct PolyboolClipper
    {
        internal static Polygon SegmentChainer(NativeList<Segment> segments, bool inverted)
        {
            var chains = new UnsafeList<UnsafeList<long2>>(16, Allocator.Temp);
            var polygon = new Polygon(segments.Length, 8, false, Allocator.Temp);

            for (int k = 0, length = segments.Length; k < length; k++)
            {
                var seg = segments[k];

                var pt1 = seg.start;
                var pt2 = seg.end;

                if (pt1 == pt2)
                {
                    Debug.Log("PolyBool: Warning: Zero-length segment detected; your epsilon is probably too small or too large");
                    continue;
                }

                // search for two chains that this segment matches
                var firstMatch = Matcher.Empty;
                var secondMatch = Matcher.Empty;
                var setFirstMatch = true;

                for (int i = 0; i < chains.Length; i++)
                {
                    ref var chain = ref chains.ElementAt(i);
                    var head = chain[0];
                    var tail = chain[^1];
                    if (head == pt1)
                    {
                        if (SetMatch(i, true, true, ref firstMatch, ref secondMatch, ref setFirstMatch))
                            break;
                    }
                    else if (head == pt2)
                    {
                        if (SetMatch(i, true, false, ref firstMatch, ref secondMatch, ref setFirstMatch))
                            break;
                    }
                    else if (tail == pt1)
                    {
                        if (SetMatch(i, false, true, ref firstMatch, ref secondMatch, ref setFirstMatch))
                            break;
                    }
                    else if (tail == pt2)
                    {
                        if (SetMatch(i, false, false, ref firstMatch, ref secondMatch, ref setFirstMatch))
                            break;
                    }
                }

                if (firstMatch == Matcher.Empty)
                {
                    // we didn't match anything, so create a new chain
                    var temp = new UnsafeList<long2>(16, Allocator.Temp)
                    {
                        pt1,
                        pt2
                    };
                    chains.Add(temp);
                    continue;
                }

                if (secondMatch == Matcher.Empty)
                {
                    // we matched a single chain

                    // add the other point to the appropriate end, and check to see if we've closed the
                    // chain into a loop

                    var index = firstMatch.index;
                    var pt = firstMatch.matchesPt1 ? pt2 : pt1; // if we matched pt1, then we add pt2, etc
                    var addToHead = firstMatch.matchesHead; // if we matched at head, then add to the head

                    ref var chain = ref chains.ElementAt(index);
                    var grow = addToHead ? chain[0] : chain[^1];
                    var grow2 = addToHead ? chain[1] : chain[^2];
                    var oppo = addToHead ? chain[^1] : chain[0];
                    var oppo2 = addToHead ? chain[^2] : chain[1];

                    if (PointUtils.IsCollinear(grow2, grow, pt))
                    {
                        // grow isn't needed because it's directly between grow2 and pt:
                        // grow2 ---grow---> pt
                        if (addToHead)
                            chain.RemoveAt(0);
                        else
                            chain.RemoveAt(chain.Length - 1);
                        grow = grow2; // old grow is gone... new grow is what grow2 was
                    }

                    if (oppo == pt)
                    {                        

                        int start = 0, end = chain.Length;
                        if (PointUtils.IsCollinear(oppo2, oppo, grow))
                        {
                            // oppo isn't needed because it's directly between oppo2 and grow:
                            // oppo2 ---oppo--->grow
                            if (addToHead)
                                end = chain.Length - 1;
                            else
                                start = 1;
                        }
                        // we have a closed chain!
                        polygon.AddComponent(chain, start, end);
                        chain.Dispose();
                        // we're closing the loop, so remove chain from chains
                        chains.RemoveRange(index, 1);
                        continue;
                    }

                    // not closing a loop, so just add it to the appropriate side
                    if (addToHead)
                    {
                        chain.InsertRange(0, 1);
						chain[0] = pt;

					}
                    else
                        chain.Add(pt);
                    continue;
                }

                int f = firstMatch.index;
                int s = secondMatch.index;

                bool reverseF = chains[f].Length < chains[s].Length; // reverse the shorter chain, if needed
                if (firstMatch.matchesHead)
                {
                    if (secondMatch.matchesHead)
                    {
                        if (reverseF)
                        {
                            // <<<< F <<<< --- >>>> S >>>>
                            chains.ElementAt(f).Reverse();
                            // >>>> F >>>> --- >>>> S >>>>
                            AppendChain(f, s);
                        }
                        else
                        {
                            // <<<< F <<<< --- >>>> S >>>>
                            chains.ElementAt(s).Reverse();
                            // <<<< F <<<< --- <<<< S <<<<   logically same as:
                            // >>>> S >>>> --- >>>> F >>>>
                            AppendChain(s, f);

                        }
                    }
                    else
                    {
                        // <<<< F <<<< --- <<<< S <<<<   logically same as:
                        // >>>> S >>>> --- >>>> F >>>>
                        AppendChain(s, f);
                    }
                }
                else
                {
                    if (secondMatch.matchesHead)
                    {
                        // >>>> F >>>> --- >>>> S >>>>
                        AppendChain(f, s);
                    }
                    else
                    {
                        if (reverseF)
                        {
                            // >>>> F >>>> --- <<<< S <<<<
                            chains.ElementAt(f).Reverse();
                            // <<<< F <<<< --- <<<< S <<<<   logically same as:
                            // >>>> S >>>> --- >>>> F >>>>
                            AppendChain(s, f);
                        }
                        else
                        {
                            // >>>> F >>>> --- <<<< S <<<<
                            chains.ElementAt(s).Reverse();
                            // >>>> F >>>> --- >>>> S >>>>
                            AppendChain(f, s);
                        }
                    }
                }
            }
			//for (int i = 0, ii = chains.Length; i < ii; i++)
			//{
			//	var chain = chains[i];
			//	if (chain.Length > 0)
			//	{
			//		polygon.startIDs.Add(polygon.nodes.Length);
			//		polygon.nodes.CopyFrom(chain);
			//	}
			//}
			polygon.ClosePolygon();//abuse last startID to store end of last component

            if (polygon.startIDs.Length > 1)
            {
                //fix orientation of regions to conform to postscript: outer contours CCW, holes CW

                var startIDs = polygon.startIDs;
                var nodes = polygon.nodes;

                //first identify largest region, assuming this is the outer contour
                Span<double> orientations = stackalloc double[startIDs.Length - 1];

                var maxRegion = 0;
                double maxArea = 0;
                for (int i = 0, length = startIDs.Length - 1; i < length; i++)
                {
                    int start = startIDs[i];
                    int end = startIDs[i + 1];
                    orientations[i] = nodes.SignedArea(start, end);
                    var area = math.abs(orientations[i]);
                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxRegion = i;
                    }
                }
                //second ensure the identified outer contour is CCW (=postscript convention)
                if (orientations[maxRegion] < 0) //when it is CW, then reverse
                {
                    polygon.Reverse(maxRegion);
                    orientations[maxRegion] = -orientations[maxRegion]; //flip sign of oriention now that it is reversed
                }

                var largestRegionStartID = startIDs[maxRegion];
                var largestRegionEndID = startIDs[maxRegion + 1];

                //third, for all regions inside the identified outer contour, ensure they are CW
                for (int i = 0, length = startIDs.Length - 1; i < length; i++)
                {
                    if (i == maxRegion)
                        continue;

                    //for the first node of this component, check if it is inside the identified outer contour
                    var isInside = polygon.PnInPolyFranklin(nodes[startIDs[i]], largestRegionStartID, largestRegionEndID, false);
                    if (isInside)
                    {
                        if (orientations[i] > 0) //when it is CCW, then reverse
                        {
                            polygon.Reverse(i);
                            orientations[i] = -orientations[i]; //flip sign of oriention now that it is reversed
                        }
                    }
                    else //if not inside, it is probably another "out contour"...so ensure this is also CCW
                    {
                        if (orientations[i] < 0)
                        {
                            polygon.Reverse(i);
                            orientations[i] = -orientations[i]; //flip sign of oriention now that it is reversed
                        }
                    }
                }
            }

			for (int i = 0, end = chains.Length; i < end; i++)
			{
				var chain = chains[i];
				if (chain.IsCreated)
					chains[i].Dispose();
			}
			chains.Dispose();
			return polygon;

            void AppendChain(int index1, int index2)
            {
                // index1 gets index2 appended to it, and index2 is removed
                ref var chain1 = ref chains.ElementAt(index1);
                ref var chain2 = ref chains.ElementAt(index2);
                var tail = chain1[^1];
                var tail2 = chain1[^2];
                var head = chain2[0];
                var head2 = chain2[1];
                int chain2start = 0;

                if (PointUtils.IsCollinear(tail2, tail, head))
                {
                    // tail isn't needed because it's directly between tail2 and head
                    // tail2 ---tail---> head
                    chain1.RemoveAt(chain1.Length - 1);
                    tail = tail2; // old tail is gone... new tail is what tail2 was
                }

                if (PointUtils.IsCollinear(tail, head, head2))
                {
                    // head isn't needed because it's directly between tail and head2
                    // tail ---head---> head2
                    chain2start = 1;
                }
                for (int i = chain2start, length = chain2.Length; i < length; i++)
                    chain1.Add(chain2[i]);
                chain2.Dispose();
                chains.RemoveRange(index2, 1);
            }
        }
        static bool SetMatch(int index, bool matchesHead, bool matchesPt1, ref Matcher firstMatch, ref Matcher secondMatch, ref bool setFirstMatch)
        {
            if (setFirstMatch)
            {
                firstMatch.index = index;
                firstMatch.matchesHead = matchesHead;
                firstMatch.matchesPt1 = matchesPt1;
                setFirstMatch = false;
                return false;
            }
            secondMatch.index = index;
            secondMatch.matchesHead = matchesHead;
            secondMatch.matchesPt1 = matchesPt1;
            return true; // we've matched twice, we're done here
        }       
    }
}