using Unity.Collections;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{
    public partial struct PolyboolClipper
    {
        // core API
        public static PolySegments Segments(Polygon poly, ref Intersecter intersecter)
        {
            intersecter.Reset(true);
            for (int k = 0, length = poly.startIDs.Length - 1; k < length; k++)
            {
                int start = poly.startIDs[k];
                int end = poly.startIDs[k + 1];
                intersecter.AddRegion(poly, start, end);
            }
            var result = intersecter.Calculate(poly.inverted, false);
            var polySegments = new PolySegments { segments = result, inverted = poly.inverted };
            return polySegments;
        }

        public static CombinedPolySegments Combine(PolySegments segments1, PolySegments segments2, ref Intersecter intersecter)
        {
            intersecter.Reset(false);
            intersecter.AddSegments(segments1.segments, true, segments2.segments, false);
            var result = intersecter.Calculate(segments1.inverted, segments2.inverted);
            var combinedSegments = new CombinedPolySegments(result, segments1.inverted, segments2.inverted);
            return combinedSegments;
        }
        public static PolySegments SelectUnion(CombinedPolySegments combined)
        {
            NativeList<Segment> segments;
            if (combined.inverted1)
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.Intersection);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.Difference);
            }
            else
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.DifferenceRev);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.Union);
            }

            var result = new PolySegments
            {
                segments = segments,
                inverted = combined.inverted1 || combined.inverted2
            };
            return result;
        }
        public static PolySegments SelectIntersect(CombinedPolySegments combined)
        {
            NativeList<Segment> segments;
            if (combined.inverted1)
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.Union);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.DifferenceRev);
            }
            else
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.Difference);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.Intersection);
            }

            var result = new PolySegments
            {
                segments = segments,
                inverted = combined.inverted1 && combined.inverted2
            };
            return result;
        }
        public static PolySegments SelectDifference(CombinedPolySegments combined)
        {
            NativeList<Segment> segments;
            if (combined.inverted1)
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.DifferenceRev);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.Union);
            }
            else
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.Intersection);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.Difference);
            }

            var result = new PolySegments
            {
                segments = segments,
                inverted = combined.inverted1 && !combined.inverted2
            };
            return result;
        }
        public static PolySegments SelectDifferenceRev(CombinedPolySegments combined)
        {
            NativeList<Segment> segments;
            if (combined.inverted1)
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.Difference);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.Intersection);
            }
            else
            {
                if (combined.inverted2)
                    segments = SegmentSelector.Select(combined.combined, ClipType.Union);
                else
                    segments = SegmentSelector.Select(combined.combined, ClipType.DifferenceRev);
            }

            var result = new PolySegments
            {
                segments = segments,
                inverted = !combined.inverted1 && combined.inverted2
            };
            return result;
        }
        public static PolySegments SelectXor(CombinedPolySegments combined)
        {
            var segments = SegmentSelector.Select(combined.combined, ClipType.Xor);            

            var result = new PolySegments
            {
                segments = segments,
                inverted = combined.inverted1 != combined.inverted2
            };
            return result;
        }

        public static Polygon Operate(Polygon poly1, Polygon poly2, ClipType clipType, FillRule fillRule)
        {
            var size = poly1.nodes.Length + poly2.nodes.Length;
            var intersecter = new Intersecter(true, size, fillRule);
            var seg1 = Segments(poly1, ref intersecter);
            var seg2 = Segments(poly2, ref intersecter);
            var comb = Combine(seg1, seg2, ref intersecter);
            PolySegments seg3 = default;
            switch(clipType)
            {
                case ClipType.Union:
                    seg3 = SelectUnion(comb);
                    return new Polygon(seg3);
                case ClipType.Intersection:
                    seg3 = SelectIntersect(comb);
                    return new Polygon(seg3);
                case ClipType.Difference:
                    seg3 = SelectDifference(comb);
                    return new Polygon(seg3);
                case ClipType.DifferenceRev:
                    seg3 = SelectDifferenceRev(comb);
                    return new Polygon(seg3);
                case ClipType.Xor:
                    seg3 = SelectXor(comb);
                    return new Polygon(seg3);
                default:
                    return new Polygon(0,0, false, Allocator.Temp);
            }            
        }

        // helper functions for common operations
        public static Polygon Union(Polygon poly1, Polygon poly2, FillRule fillRule)
        {
            var size = poly1.nodes.Length + poly2.nodes.Length;
            var intersecter = new Intersecter(true, size, fillRule);
            var seg1 = Segments(poly1, ref intersecter);
            var seg2 = Segments(poly2, ref intersecter);
            var comb = Combine(seg1, seg2, ref intersecter);
            var seg3 = SelectUnion(comb);
            return new Polygon(seg3);
        }
        public static Polygon Intersect(Polygon poly1, Polygon poly2, FillRule fillRule)
        {
            var size = poly1.nodes.Length + poly2.nodes.Length;
            var intersecter = new Intersecter(true, size, fillRule);
            var seg1 = Segments(poly1, ref intersecter);
            var seg2 = Segments(poly2, ref intersecter);
            var comb = Combine(seg1, seg2, ref intersecter);
            var seg3 = SelectIntersect(comb);
            return new Polygon(seg3);
        }
        public static Polygon Difference(Polygon poly1, Polygon poly2, FillRule fillRule)
        {
            var size = poly1.nodes.Length + poly2.nodes.Length;
            var intersecter = new Intersecter(true, size, fillRule);
            var seg1 = Segments(poly1, ref intersecter);
            var seg2 = Segments(poly2, ref intersecter);
            var comb = Combine(seg1, seg2, ref intersecter);
            var seg3 = SelectDifference(comb);
            return new Polygon(seg3);
        }
        public static Polygon DifferenceRev(Polygon poly1, Polygon poly2, FillRule fillRule)
        {
            var size = poly1.nodes.Length + poly2.nodes.Length;
            var intersecter = new Intersecter(true, size, fillRule);
            var seg1 = Segments(poly1, ref intersecter);
            var seg2 = Segments(poly2, ref intersecter);
            var comb = Combine(seg1, seg2, ref intersecter);
            var seg3 = SelectDifferenceRev(comb);
            return new Polygon(seg3);
        }
        public static Polygon Xor(Polygon poly1, Polygon poly2, FillRule fillRule)
        {
            var size = poly1.nodes.Length + poly2.nodes.Length;
            var intersecter = new Intersecter(true, size, fillRule);
            var seg1 = Segments(poly1, ref intersecter);
            var seg2 = Segments(poly2, ref intersecter);
            var comb = Combine(seg1, seg2, ref intersecter);
            var seg3 = SelectXor(comb);
            return new Polygon(seg3);
        }
    }
}