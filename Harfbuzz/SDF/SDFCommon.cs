using System.IO;
using Unity.Collections;
using Unity.Mathematics;

namespace HarfBuzz.SDF
{
    public static class SDFCommon
    {
        public readonly static bool USE_SQUARED_DISTANCES = false;
        public const int DEFAULT_SPREAD = 8;
        public const int MIN_SPREAD = 2;
        public const int MAX_SPREAD = 32;
        public const int MAX_NEWTON_STEPS = 4;
        public const int MAX_NEWTON_DIVISIONS = 4;
        public const int FT_TRIG_SAFE_MSB = 29;

        public static void WriteGlyphOutlineToFile(string path, in NativeList<SDFEdge> edges)
        {
            if(edges.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            SDFEdge edge;
            edge = edges[0];
            writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.end_pos.x} {edge.end_pos.y}");              
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, in NativeArray<float> minDistances)
        {
            if (minDistances.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = minDistances.Length; i < end; i++)
            {
                writer.WriteLine($"{minDistances[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteGlyphOutlineToFile(string path, BezierData bezierData)
        {
            var edges = bezierData.edges;
            var contourIDs= bezierData.contourIDs;
            if (contourIDs.Length < 2 || edges.Length == 0)
                return;

            StreamWriter writer = new StreamWriter(path, false);
            SDFEdge edge;
            for (int contourID = 0, end= contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    edge = edges[edgeID];
                    writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y},");
                }
                edge = edges[nextStartID - 1];
                writer.WriteLine($"{edge.end_pos.x} {edge.end_pos.y},");                
                writer.WriteLine();
            }
            writer.Close();
        }
    }
}
