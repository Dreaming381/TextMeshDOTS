using AOT;
using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace HarfBuzz.SDF
{

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ReleaseDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MoveToDelegate(IntPtr dfuncs, ref BezierData data, ref DrawState st, float to_x, float to_y, IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void QuadraticToDelegate(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control_x, float control_y, float to_x, float to_y, IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CubicToDelegate(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control1_x, float control1_y, float control2_x, float control2_y, float to_x, float to_y, IntPtr user_data);


    public static unsafe class HBDelegateProxies
    {
        [MonoPInvokeCallback(typeof(ReleaseDelegate))]
        public static void Test()
        {
            //Debug.Log($"harfbuzz blob called this delegate upon destroying blob ");
        }
        public static void HBDraw_MoveTo(IntPtr dfuncs, ref BezierData data, ref DrawState st, float to_x, float to_y, IntPtr user_data )
        {
            data.contourIDs.Add(data.edges.Length);

            if(data.edges.Length > 1)
                data.edges.ElementAt(data.edges.Length - 1).nextId = -1;
            //Debug.Log($"Move to {to_x} {to_y}");
            //Debug.Log($"Open? {st.path_open} {st.path_start_x} {st.path_start_y} {st.current_x} {st.current_y}");
        }

        public static void HBDraw_LineTo(IntPtr dfuncs, ref BezierData data, ref DrawState st, float to_x, float to_y, IntPtr user_data)
        {
            //Debug.Log($"Line from {st.current_x},{st.current_y} to {to_x},{to_y}");
            var edge = new SDFEdge
            {
                start_pos = new float2(st.current_x, st.current_y),
                end_pos = new float2(to_x, to_y),
                control1 = default,
                control2 = default,
                edge_type = SDFEdgeType.LINE,
                nextId = data.edges.Length + 1 //this assumes that the contour continues. For last contour point (MoveTocall) this is set to -1
            };
            var edgeBBox = BBox.GetLineBBox(edge.start_pos, edge.end_pos);

            data.edges.Add(edge);
            data.glyphRect= BBox.Union(data.glyphRect, edgeBBox);
        }

        public static void HBDraw_QuadraticTo(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control_x, float control_y, float to_x, float to_y, IntPtr user_data)
        {
            //Debug.Log($"Quadratic add control {control_x} {control_y}");
            //Debug.Log($"Quadratic from {st.current_x},{st.current_y}  to {to_x}, {to_y}");
            var edge = new SDFEdge
            {
                start_pos = new float2(st.current_x, st.current_y),
                end_pos = new float2(to_x, to_y),
                control1 = new float2(control_x, control_y),
                control2 = default,
                edge_type = SDFEdgeType.QUADRATIC,
                nextId = data.edges.Length + 1 //this assumes that the contour continues. For last contour point (MoveTocall) this is set to -1
            };
            var edgeBBox = BBox.GetQuadraticBezierBBox(edge.start_pos, edge.control1, edge.end_pos);

            data.edges.Add(edge);
            data.glyphRect = BBox.Union(data.glyphRect, edgeBBox);
        }

        public static void HBDraw_CubicTo(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control1_x, float control1_y, float control2_x, float control2_y, float to_x, float to_y, IntPtr user_data)
        {
            //Debug.Log($"Cubic add control 1 {control1_x} {control1_y}");
            //Debug.Log($"Cubic add control 2 {control2_x} {control2_x}");
            //Debug.Log($"Cubic from {st.current_x},{st.current_y}  to {to_x}, {to_y}");
            //Debug.Log($"Open? {st.path_open} {st.path_start_x} {st.path_start_y} {st.current_x} {st.current_y}");
            var edge = new SDFEdge
            {
                start_pos = new float2(st.current_x, st.current_y),
                end_pos = new float2(to_x, to_y),
                control1 = new float2(control1_x, control1_y),
                control2 = new float2(control2_x, control2_y),
                edge_type = SDFEdgeType.CUBIC,
                nextId = data.edges.Length + 1 //this assumes that the contour continues. For last contour point (MoveTocall) this is set to -1
            };
            var edgeBBox = BBox.GetCubicBezierBBox(edge.start_pos, edge.control1, edge.control1, edge.end_pos);

            data.edges.Add(edge);
            data.glyphRect = BBox.Union(data.glyphRect, edgeBBox);
        }
    }
}
