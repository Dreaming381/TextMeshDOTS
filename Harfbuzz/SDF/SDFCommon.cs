using System.IO;
using Unity.Collections;
using Unity.Mathematics;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    public static class SDFCommon
    {
        /// <summary> Max permitted deviatition of generated lines from original bezier curve. 
        /// Sensible value is fontsize (=atlas pointsize) / 25). Lower values massively hit performance.
        /// </summary>
        public const float MAX_DEVIATION_SPLITTING = 2f;
        public readonly static bool USE_SQUARED_DISTANCES = false;
        public const int DEFAULT_SPREAD = 8;
        public const int MIN_SPREAD = 2;
        public const int MAX_SPREAD = 32;
        public const int MAX_NEWTON_STEPS = 4;
        public const int MAX_NEWTON_DIVISIONS = 4;
        public const int FT_TRIG_SAFE_MSB = 29;
		
		public static void CenterGlyphInGlyphRect(ref DrawData drawData, int width, int height, int padding)
		{
			var edges = drawData.edges;
			var shiftx = -drawData.glyphRect.min.x + ((width - (drawData.glyphRect.width + 2 * padding)) / 2);
            var shifty = -drawData.glyphRect.min.y + ((height -(drawData.glyphRect.height + 2 * padding)) / 2);
            float2 shift = new float2(shiftx, shifty);
            for (int k = 0, kk = edges.Length; k < kk; k++)
            {
                ref var edge = ref edges.ElementAt(k);
                edge.start_pos += shift;
                edge.end_pos += shift;
                edge.control1 += shift;
                edge.control2 += shift;
                //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
            }
            ref var glyphRect = ref drawData.glyphRect;
            glyphRect.min += shift;
			glyphRect.max += shift;
		}
        public static void TransformGlyph(ref DrawData drawData, float2x3 transform)
        {
            var edges = drawData.edges;
            for (int k = 0, kk = edges.Length; k < kk; k++)
            {
                ref var edge = ref edges.ElementAt(k);
                edge.start_pos = math.mul(transform, new float3(edge.start_pos,1));
                edge.end_pos = math.mul(transform, new float3(edge.end_pos, 1));
                edge.control1 = math.mul(transform, new float3(edge.control1, 1));
                edge.control2 = math.mul(transform, new float3(edge.control2, 1));
                //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
            }
            ref var glyphRect = ref drawData.glyphRect;
            glyphRect.min = math.mul(transform, new float3(glyphRect.min, 1));
            glyphRect.max = math.mul(transform, new float3(glyphRect.max, 1));
        }

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
        public static void WriteGlyphOutlineToFile(string path, ref DrawData drawData)
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
                    writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
                    //writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y} {edge.control1.x} {edge.control1.y} {edge.end_pos.x} {edge.end_pos.y} {edge.edge_type}");
                }
                writer.WriteLine();
            }
            writer.Close();
        }
        //public static void WriteGlyphOutlineToFile(string path, ref DrawData drawData)
        //      {
        //          var edges = drawData.edges;
        //          var contourIDs= drawData.contourIDs;
        //          if (contourIDs.Length < 2 || edges.Length == 0)
        //              return;

        //          StreamWriter writer = new StreamWriter(path, false);
        //	for (int edgeID = 0, end =drawData.edges.Length ; edgeID <end ; edgeID++) //for each edge
        //	{
        //		var edge = edges[edgeID];
        //		writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y} {edge.end_pos.x} {edge.end_pos.y}");
        //	}
        //	writer.WriteLine();
        //          for (int contourID = 0, end= contourIDs.Length; contourID < end; contourID++) //for each contour
        //          {
        //		var contour = contourIDs[contourID];
        //              writer.WriteLine($"{contour}");               
        //          }
        //          writer.Close();
        //      }
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
