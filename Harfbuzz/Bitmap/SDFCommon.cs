using System.IO;
using Unity.Collections;
using Unity.Mathematics;

namespace TextMeshDOTS.HarfBuzz.Bitmap
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

        /// <summary> Max permitted deviatition of generated lines from original bezier curve. 
        /// Sensible value is fontscale / 25). Too low values massively hit performance.
        /// </summary>
        public static float GetMaxDeviation(float upem)
        {
            return math.max (2, upem / 96);
        }

        public static void WriteGlyphOutlineToFile(string path, NativeList<SDFEdge> edges)
        {
            if(edges.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            var edge = edges[0];
            writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.end_pos.x} {edge.end_pos.y}");              
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteGlyphOutlineToFile(string path, NativeList<Edge> edges)
        {
            if (edges.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            var edge = edges[0];
            
            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.x0} {edge.y0} {edge.invert}");
                writer.WriteLine($"{edge.x1} {edge.y1}");
                writer.WriteLine();
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
        public static void WriteMinDistancesToFile(string path, in NativeArray<byte> minDistances)
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
        public static void WriteGlyphOutlineToFile(string path, ref DrawData drawData, bool fullBezier=false)
        {
            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            if (contourIDs.Length < 2 || edges.Length == 0)
                return;

            StreamWriter writer = new StreamWriter(path, false);
            SDFEdge edge;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    edge = edges[edgeID];
                    if(fullBezier)
                        writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y} {edge.control1.x} {edge.control1.y} {edge.end_pos.x} {edge.end_pos.y} {edge.edge_type}");
                    else
                        writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
                    
                }
                writer.WriteLine();
            }
            writer.Close();
        }

        public static void WriteMinDistancesToFile(string path, in NativeList<SDFDebug> distanceHelper)
        {
            if (distanceHelper.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = distanceHelper.Length; i < end; i++)
            {
                var c = distanceHelper[i];
                writer.WriteLine($"{c.edge.edge_type} {c.x} {c.y} {c.overWrite} {c.pixelWasSet} previous: sign {c.previousPixelValue.sign} cross {c.previousPixelValue.cross} {c.previousPixelValue.distance} current: sign{c.pixelValue.sign} cross {c.pixelValue.cross} {c.pixelValue.distance}");
            }
            writer.WriteLine();
            writer.Close();
        }
    }
    public struct SDFDebug
    {
        public int x;
        public int y;
        public bool pixelWasSet;
        public bool overWrite;
        public SignedDistance previousPixelValue;
        public SignedDistance pixelValue;
        public SDFEdge edge;
    }
}
