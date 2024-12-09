using Unity.Collections;
using Unity.Mathematics;

namespace HarfBuzz.SDF
{
    public struct BezierData
    {
        public NativeList<SDFEdge> edges;
    }
    /// <summary>Represent an edge of a contour  </summary>
    /// <param name="start_pos">Start position of an edge.Valid for all types of edges.</param>
    /// <param name="end_pos">End position of an edge.  Valid for all types of edges.</param>
    /// <param name="control1">A control point of the edge.Valid only for <see cref="SDFEdgeType.CONIC"/> and <see cref="SDFEdgeType.CUBIC"/> </param>
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
        CONIC = 2,
        CUBIC = 3
    }
}
