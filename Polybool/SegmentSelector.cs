using Unity.Collections;

namespace TextMeshDOTS.Polybool
{
    public static class SegmentSelector
    {
		public static NativeList<Segment> Select(NativeList<Segment> segments, ClipType clipType)
		{
			var result = new NativeList<Segment>(segments.Length, Allocator.Temp);
			switch (clipType)
			{
				case ClipType.Union:
					for (int i = 0, length = segments.Length; i < length; i++)
					{
						var segment = segments[i];
						var finalAbove = segment.fillAbove | segment.fillOtherAbove;
						var finalBelow = segment.fillBelow | segment.fillOtherBelow;
						if (finalAbove ^ finalBelow)
							result.Add(new Segment { start = segment.start, end = segment.end, fillAbove = finalAbove, fillBelow = finalBelow, fillOtherAbove = false, fillOtherBelow = false});						
					}
					break;

				case ClipType.Intersection:
					for (int i = 0, length = segments.Length; i < length; i++)
					{
						var segment = segments[i];
						var finalAbove = segment.fillAbove & segment.fillOtherAbove;
						var finalBelow = segment.fillBelow & segment.fillOtherBelow;
						if (finalAbove ^ finalBelow)
							result.Add(new Segment { start = segment.start, end = segment.end, fillAbove = finalAbove, fillBelow = finalBelow, fillOtherAbove = false, fillOtherBelow = false });
					}
					break;
				case ClipType.Difference:
					for (int i = 0, length = segments.Length; i < length; i++)
					{
						var segment = segments[i];
						var finalAbove = segment.fillAbove & !segment.fillOtherAbove;
						var finalBelow = segment.fillBelow & !segment.fillOtherBelow;
						if (finalAbove ^ finalBelow)
							result.Add(new Segment { start = segment.start, end = segment.end, fillAbove = finalAbove, fillBelow = finalBelow, fillOtherAbove = false, fillOtherBelow = false });
					}
					break;
				case ClipType.DifferenceRev:
					for (int i = 0, length = segments.Length; i < length; i++)
					{
						var segment = segments[i];
						var finalAbove = !segment.fillAbove & segment.fillOtherAbove;
						var finalBelow = !segment.fillBelow & segment.fillOtherBelow;
						if (finalAbove ^ finalBelow)
							result.Add(new Segment { start = segment.start, end = segment.end, fillAbove = finalAbove, fillBelow = finalBelow, fillOtherAbove = false, fillOtherBelow = false });
					}
					break;
				case ClipType.Xor:
					for (int i = 0, length = segments.Length; i < length; i++)
					{
						var segment = segments[i];
						var finalAbove = segment.fillAbove ^ segment.fillOtherAbove;
						var finalBelow = segment.fillBelow ^ segment.fillOtherBelow;
						if (finalAbove ^ finalBelow)
							result.Add(new Segment { start = segment.start, end = segment.end, fillAbove = finalAbove, fillBelow = finalBelow, fillOtherAbove = false, fillOtherBelow = false });
					}
					break;
				default:
					break;
			}
			return result;
		}		
	}
}