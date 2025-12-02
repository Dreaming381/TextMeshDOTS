using TextMeshDOTS.Clipper2AoS;
using Unity.Collections;
using Unity.Mathematics;

namespace TextMeshDOTS.HarfBuzz
{
    internal class PolygonOperation
    {
        /// <summary> Use BURST compatible Clipper2 library to do a union of polygon on itself to remove self-intersections.  </summary>
        public static void RemoveSelfIntersections(ref DrawData subject, ClipType cliptype, FillRule fillrule)
        {
            ClipperL clipper = new ClipperL(Allocator.Temp);
            var nodes = subject.edges;
            var contourIDs = subject.contourIDs;
            var scale = 1000;
            var invScale = 1f / scale;

            var subjectNodes = new NativeList<int2>(nodes.Length, Allocator.Temp);
            for (int i = 0, length = contourIDs.Length - 1; i < length; i++)
            {
                int start = contourIDs[i];
                int end = contourIDs[i + 1];
                for (int k = start; k < end; k++)
                    subjectNodes.Add(new int2((int)(nodes[k].start_pos.x * scale), (int)(nodes[k].start_pos.y * scale)));
            }
            clipper.AddSubject(subjectNodes.AsArray(), contourIDs.AsArray());

            var solutionNodesClosed = new NativeList<int2>(nodes.Length, Allocator.Temp);
            var solutionStartIDsClosed = new NativeList<int>(contourIDs.Length, Allocator.Temp);
            var solutionNodesOpen = new NativeList<int2>(0, Allocator.Temp);
            var solutionStartIDsOpen = new NativeList<int>(0, Allocator.Temp);
            clipper.Execute(cliptype, fillrule, ref solutionNodesClosed, ref solutionStartIDsClosed, ref solutionNodesOpen, ref solutionStartIDsOpen);

            if (solutionStartIDsClosed.Length < 2)
                return;

            nodes.Clear();
            contourIDs.Clear();
            for (int i = 0, length = solutionStartIDsClosed.Length - 1; i < length; i++)
            {
                contourIDs.Add(nodes.Length);
                int start = solutionStartIDsClosed[i];
                int end = solutionStartIDsClosed[i + 1];
                for (int k = start; k < end - 1; k++)
                {
                    var startPos = ((float2)solutionNodesClosed[k]) * invScale;
                    var endPos = ((float2)solutionNodesClosed[k + 1]) * invScale;
                    nodes.Add(new SDFEdge { start_pos = startPos, end_pos = endPos, edge_type = SDFEdgeType.LINE });
                }
            }
            contourIDs.Add(nodes.Length);
            clipper.Dispose();
        }

        ///// <summary> Use Clipper2 library to do a union of polygon on itself to remove self-intersections.  </summary>
        //public static void RemoveSelfIntersections(ref DrawData subject, ClipType cliptype, FillRule fillrule, out DrawData solutionClosed)
        //{
        //    ClipperD clipper = new ClipperD();
        //    var nodes = subject.edges;
        //    var startIDs = subject.contourIDs;
        //    //PathsD paths = new PathsD();
        //    for (int i = 0, length = startIDs.Length - 1; i < length; i++)
        //    {
        //        int start = startIDs[i];
        //        int end = startIDs[i + 1];
        //        var path = new PathD(end - start);
        //        for (int k = start; k < end; k++)
        //            path.Add(new PointD(nodes[k].start_pos.x, nodes[k].start_pos.y));
        //        clipper.AddSubject(path);
        //        //paths.Add(path);
        //    }

        //    solutionClosed = new DrawData(subject.edges.Length, subject.contourIDs.Length, subject.maxDeviation, Allocator.Temp);
        //    //var solutionPathClosed = Clipper.InflatePaths(paths, 0.1, JoinType.Bevel, EndType.Butt, 2, 2, 0);
        //    var solutionPathClosed = new PathsD();
        //    var solutionPathOpen = new PathsD();
        //    clipper.Execute(cliptype, fillrule, solutionPathClosed, solutionPathOpen);

        //    for (int i = 0, length = solutionPathClosed.Count; i < length; i++)
        //    {
        //        var path = solutionPathClosed[i];
        //        var firstStartPos = path[0];
        //        var lastStartPos = path[^1];
        //        for (int k = 0, kk = path.Count - 1; k < kk; k++)
        //        {
        //            var startPos = path[k];
        //            var endPos = path[k + 1];
        //            solutionClosed.edges.Add(new SDFEdge { start_pos = new float2((float)startPos.x, (float)startPos.y), end_pos = new float2((float)endPos.x, (float)endPos.y), edge_type = SDFEdgeType.LINE });
        //        }
        //        solutionClosed.edges.Add(new SDFEdge { start_pos = new float2((float)lastStartPos.x, (float)lastStartPos.y), end_pos = new float2((float)firstStartPos.x, (float)firstStartPos.y), edge_type = SDFEdgeType.LINE });

        //        ////close polygon
        //        //if (path[^1] != firstStartPos)
        //        //    solutionClosed.edges.Add(new SDFEdge { start_pos = new float2((float)firstStartPos.x, (float)firstStartPos.y) });
        //        solutionClosed.contourIDs.Add(solutionClosed.edges.Length);
        //    }
        //    solutionClosed.maxDeviation = subject.maxDeviation;
        //    solutionClosed.glyphRect = subject.glyphRect;
        //}

        ///// <summary> User PolyBool library to do a union on itself to remove self-intersections.  </summary>
        //public static void RemoveSelfIntersectionsPolyBool(ref DrawData subject, Polybool.ClipType cliptype, out DrawData solutionClosed)
        //{
        //    var nodes = subject.edges;
        //    var startIDs = subject.contourIDs;
        //    PolyboolPolygon subject2 = new PolyboolPolygon();
        //    subject2.polygon = new Polygon(subject.edges.Length, Allocator.Temp);
        //    var subject2Nodes = subject2.polygon.nodes;
        //    var subject2StartIDs = subject2.polygon.startIDs;
        //    subject2StartIDs.Add(subject2Nodes.Length);

        //    for (int i = 0, length = startIDs.Length - 1; i < length; i++)
        //    {
        //        int start = startIDs[i];
        //        int end = startIDs[i + 1];
        //        for (int k = start; k < end; k++)
        //            subject2Nodes.Add(new double2(nodes[k].start_pos.x, nodes[k].start_pos.y));
        //        subject2Nodes.Add(new double2(nodes[end-1].end_pos.x, nodes[end-1].end_pos.y));
        //        subject2StartIDs.Add(subject2Nodes.Length);
        //    }

        //    subject2.inverted = false;

        //    PolyboolPolygon clip = new PolyboolPolygon();
        //    clip.polygon = new Polygon(0, Allocator.Temp);
        //    clip.inverted = false;
        //    var result = PolyboolClipper.Operate(subject2, clip, cliptype);

        //    var resultPolygonNodes= result.polygon.nodes;
        //    var resultPolygonStartIDs = result.polygon.startIDs;
        //    solutionClosed = new DrawData(resultPolygonNodes.Length, resultPolygonStartIDs.Length, subject.maxDeviation, Allocator.Temp);
        //    for (int i = 0, length = resultPolygonStartIDs.Length - 1; i < length; i++)
        //    {
        //        int start = resultPolygonStartIDs[i];
        //        int end = resultPolygonStartIDs[i + 1];

        //        var firstStartPos = resultPolygonNodes[start];
        //        var lastStartPos = resultPolygonNodes[end - 1];
        //        for (int k = start; k < end - 1; k++)                
        //        {
        //            var startPos = resultPolygonNodes[k];
        //            var endPos = resultPolygonNodes[k + 1];
        //            solutionClosed.edges.Add(new SDFEdge { start_pos = new float2((float)startPos.x, (float)startPos.y), end_pos = new float2((float)endPos.x, (float)endPos.y), edge_type = SDFEdgeType.LINE });
        //        }
        //        solutionClosed.edges.Add(new SDFEdge { start_pos = new float2((float)lastStartPos.x, (float)lastStartPos.y), end_pos = new float2((float)firstStartPos.x, (float)firstStartPos.y), edge_type = SDFEdgeType.LINE });
        //        solutionClosed.contourIDs.Add(solutionClosed.edges.Length);
        //    }
        //    solutionClosed.maxDeviation = subject.maxDeviation;
        //    solutionClosed.glyphRect = subject.glyphRect;
        //}
    }
}