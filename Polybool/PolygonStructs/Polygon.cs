using NUnit;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{
    public struct Polygon
    {
        public NativeList<double2> nodes;
        public NativeList<int> startIDs;
        public bool inverted;

        public Polygon(int nodeCount, int startIDCount, bool inverted, Allocator allocator)
        {
            nodes = new NativeList<double2>(nodeCount, allocator);
            startIDs = new NativeList<int>(startIDCount, allocator);
            this.inverted = inverted;
        }
        public Polygon(NativeList<double2> nodes, NativeList<int> startIDs, bool inverted)
        {
            this.nodes = nodes;
            this.startIDs = startIDs;
            this.inverted = inverted;
        }

        public Polygon(PolySegments segments)
        {
            var result = PolyboolClipper.SegmentChainer(segments.segments, segments.inverted);
            nodes = result.nodes;
            startIDs = result.startIDs;
            this.inverted = result.inverted;
        }

        public void AddComponent(UnsafeList<double2> points, int start, int end)
        {
            if (points.Length == 0)
                return;
            startIDs.Add(this.nodes.Length);
            for (int i = start; i < end; i++)
                nodes.Add(points[i]);
        }
        public void Reverse(int componentID)
        {
            var startID = startIDs[componentID];
            var endID = startIDs[componentID + 1];
            int i = startID, j = endID - 1;
            double2 temp;
            while (i < j)
            {
                temp = nodes[i];
                nodes[i] = nodes[j];
                nodes[j] = temp;
                i++;
                j--;
            }
        }
        public void Reverse(int startID, int endID)
        {
            int i = startID, j = endID;
            double2 temp;
            while (i < j)
            {
                temp = nodes[i];
                nodes[i] = nodes[j];
                nodes[j] = temp;
                i++;
                j--;
            }
        }
        /// <summary>
        /// PNPOLY, WR Franklin, keeping track of whether the number of edges crossed are even or odd. 0 means even and 1 means odd
        /// </summary>
        public bool PnInPolyFranklin(double2 p, int start, int end, bool isInside)
        {
            for (int i = start, j = end - 1; i < end; j = i++) //from (0, prev) until (end, prev)
            {
                var Pi = nodes[i];
                var Pj = nodes[j];
                if (((Pi.y > p.y) != (Pj.y > p.y)) && (p.x < (Pj.x - Pi.x) * (p.y - Pi.y) / (Pj.y - Pi.y) + Pi.x))
                    isInside = !isInside;
            }
            return isInside;
        }
        public void ClosePolygon()
        {
            startIDs.Add(this.nodes.Length);
        }
    }
}