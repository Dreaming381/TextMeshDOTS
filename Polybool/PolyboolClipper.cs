using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{
    public partial struct PolyboolClipper
    {
        // core API
        public static PolySegments Segments(PolyboolPolygon poly, ref Intersecter intersecter)
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
        public static PolySegments Select(CombinedPolySegments combined, ClipType clipType)
        {
            var result = new PolySegments
            {
                segments = SegmentSelector.Select(combined.combined, clipType),
                inverted = combined.inverted1 || combined.inverted2
            };
            return result;
        }

        public static PolyboolPolygon GetPolygon(PolySegments segments)
        {
            var result = SegmentChainer(segments.segments);
            result.inverted = segments.inverted;
            return result;
        }
        public static PolyboolPolygon Operate(PolyboolPolygon poly1, PolyboolPolygon poly2, ClipType clipType, FillRule fillRule)
        {
            var size = poly1.nodes.Length + poly2.nodes.Length;
            var intersecter = new Intersecter(true, size, fillRule);
            var seg1 = Segments(poly1, ref intersecter);
            var seg2 = Segments(poly2, ref intersecter);
            var comb = Combine(seg1, seg2, ref intersecter);
            var seg3 = Select(comb, clipType);
            return GetPolygon(seg3);
        }
    }
}