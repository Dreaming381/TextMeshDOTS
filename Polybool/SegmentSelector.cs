using Unity.Collections;

namespace TextMeshDOTS.Polybool
{
    public static class SegmentSelector
    {
        public static NativeList<Segment> Select(NativeList<Segment> segments, ClipType clipType)
        {
            var result = new NativeList<Segment>(segments.Length, Allocator.Temp);
            bool above, below, otherAbove, otherBelow, finalAbove, finalBelow;

            for (int i = 0, length = segments.Length; i < length; i++)
            {
                var segment = segments[i];
                above = segment.above == FillStatus.Filled;
                below = segment.below == FillStatus.Filled;
                otherAbove = segment.otherAbove == FillStatus.Filled;
                otherBelow = segment.otherBelow == FillStatus.Filled;
                finalAbove = false;
                finalBelow = false;

                switch (clipType)
                {
                    case ClipType.Union:
                        finalAbove = above | otherAbove;
                        finalBelow = below | otherBelow;
                        break;
                    case ClipType.Intersection:
                        finalAbove = above & otherAbove;
                        finalBelow = below & otherBelow;
                        break;
                    case ClipType.Difference:
                        finalAbove = above & !otherAbove;
                        finalBelow = below & !otherBelow;
                        break;
                    case ClipType.DifferenceRev:
                        finalAbove = !above & otherAbove;
                        finalBelow = !below & otherBelow;
                        break;
                    case ClipType.Xor:
                        finalAbove = above ^ otherAbove;
                        finalBelow = below ^ otherBelow;
                        break;
                }

                if (finalAbove ^ finalBelow)
                {
                    result.Add(new Segment
                    {
                        p0_start = segment.p0_start,
                        p1_end = segment.p1_end,
                        above = finalAbove ? FillStatus.Filled : FillStatus.NotFilled,
                        below = finalBelow ? FillStatus.Filled : FillStatus.NotFilled,
                        otherAbove = FillStatus.Undefined,
                        otherBelow = FillStatus.Undefined,
                    });
                }
            }
            return result;
        }

        //public static List<Segment> Select(List<Segment> segments, ClipType clipType)
        //{
        //    List<Segment> result = new List<Segment>();
        //    int[] selection;
        //    switch (clipType)
        //    {
        //        case ClipType.Union: selection = union; break;
        //        case ClipType.Intersection: selection = intersect; break;
        //        case ClipType.Difference: selection = difference; break;
        //        case ClipType.DifferenceRev: selection = differenceRev; break;
        //        case ClipType.Xor: selection = xor; break;
        //        default: selection = intersect; break;
        //    }

        //    for (int i = 0, length = segments.Count; i < length; i++)
        //    {
        //        var segment = segments[i];
        //        int index = (segment.above == FillStatus.Filled ? 8 : 0) +
        //                    (segment.below == FillStatus.Filled ? 4 : 0) +
        //                    (segment.otherAbove == FillStatus.Filled ? 2 : 0) +
        //                    (segment.otherBelow == FillStatus.Filled ? 1 : 0);

        //        if (selection[index] != 0)
        //        {
        //            result.Add(new Segment
        //            {
        //                p0_start = segment.p0_start,
        //                p1_end = segment.p1_end,
        //                above = selection[index] == 1 ? FillStatus.Filled : FillStatus.NotFilled, // 1 if filled above
        //                below = selection[index] == 2 ? FillStatus.Filled : FillStatus.NotFilled, // 2 if filled below
        //                otherAbove = FillStatus.Undefined,
        //                otherBelow = FillStatus.Undefined,
        //            });
        //        }
        //    }
        //    return result;
        //}
        //static readonly int[] union = {
        //        0, 2, 1, 0,
        //        2, 2, 0, 0,
        //        1, 0, 1, 0,
        //        0, 0, 0, 0
        //    };
        //static readonly int[] intersect = {
        //        0, 0, 0, 0,
        //        0, 2, 0, 2,
        //        0, 0, 1, 1,
        //        0, 2, 1, 0
        //    };
        //static readonly int[] difference = {
        //        0, 0, 0, 0,
        //        2, 0, 2, 0,
        //        1, 1, 0, 0,
        //        0, 1, 2, 0
        //    };
        //static readonly int[] differenceRev = {
        //        0, 2, 1, 0,
        //        0, 0, 1, 1,
        //        0, 2, 0, 2,
        //        0, 0, 0, 0
        //    };
        //static readonly int[] xor = {
        //        0, 2, 1, 0,
        //        2, 0, 0, 1,
        //        1, 0, 0, 2,
        //        0, 1, 2, 0
        //    };

    }
}