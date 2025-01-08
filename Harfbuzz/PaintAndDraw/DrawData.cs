using Unity.Collections;
using Unity.Mathematics;

namespace TextMeshDOTS.HarfBuzz
{
    public struct DrawData
    {
        public BBox glyphRect;
        public NativeList<SDFEdge> edges;
        /// <summary> list of first indices of a new contour. Use last index to store length of edges list for easier iteration</summary>
        public NativeList<int> contourIDs;
        public DrawData(int edgeCapacity, int contourCapacity, Allocator allocator)
        {
            edges= new NativeList<SDFEdge>(edgeCapacity, allocator);
            contourIDs = new NativeList<int>(contourCapacity, allocator);
            glyphRect = BBox.Empty;
			contourIDs.Add(0);
        }
        public void Clear()
        {
            glyphRect = BBox.Empty;
            edges.Clear();
            contourIDs.Clear();
			contourIDs.Add(0);
        }
        public void Dispose()
        {
            if (edges.IsCreated) edges.Dispose();
            if (contourIDs.IsCreated) contourIDs.Dispose();
        }

    }
    /// <summary>Represent an edge of a contour  </summary>
    /// <param name="start_pos">Start position of an edge.Valid for all types of edges.</param>
    /// <param name="end_pos">End position of an edge.  Valid for all types of edges.</param>
    /// <param name="control1">A control point of the edge.Valid only for <see cref="SDFEdgeType.QUADRATIC"/> and <see cref="SDFEdgeType.CUBIC"/> </param>
    /// <param name="control2">A control point of the edge.Valid only for <see cref="SDFEdgeType.CUBIC"/> </param>
    /// <param name="edge_type">Type of the edge, see <see cref="SDFEdgeType"/> for all possible edge types. </param>   
    public struct SDFEdge
    {
        public float2 start_pos;
        public float2 end_pos;
        public float2 control1;
        public float2 control2;
        public SDFEdgeType edge_type;
        public SDFEdge(float2 start_pos, float2 end_pos, float2 control1, float2 control2, SDFEdgeType edge_type)
        {
            this.start_pos = start_pos; 
            this.end_pos = end_pos;
            this.control1 = control1;
            this.control2 = control2;
            this.edge_type = edge_type;
        }
    }
    public enum SDFEdgeType : byte
    {
        UNDEFINED = 0,
        LINE = 1,
        QUADRATIC = 2,
        CUBIC = 3
    }
}