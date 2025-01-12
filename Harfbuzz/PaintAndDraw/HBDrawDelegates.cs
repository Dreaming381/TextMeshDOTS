using AOT;
using System;
using Unity.Burst;
using Unity.Mathematics;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    [BurstCompile]
    public struct DrawDelegates : IDisposable
    {
        public IntPtr ptr;
        public DrawDelegates(bool dummyProperty)
        {
            ptr = HB.hb_draw_funcs_create();
            FunctionPointer<MoveToDelegate> moveToFunctionPointer = BurstCompiler.CompileFunctionPointer<MoveToDelegate>(HB_draw_move_to_func_t);
            FunctionPointer<MoveToDelegate> lineToFunctionPointer = BurstCompiler.CompileFunctionPointer<MoveToDelegate>(HB_draw_move_to_func_t);
            FunctionPointer<QuadraticToDelegate> quadraticToFunctionPointer = BurstCompiler.CompileFunctionPointer<QuadraticToDelegate>(HB_draw_quadratic_to_func_t);
            FunctionPointer<CubicToDelegate> cubicToFunctionPointer = BurstCompiler.CompileFunctionPointer<CubicToDelegate>(HB_draw_cubic_to_func_t);
            FunctionPointer<CloseDelegate> closeFunctionPointer = BurstCompiler.CompileFunctionPointer<CloseDelegate>(HB_draw_close_path_func_t);
            FunctionPointer<ReleaseDelegate> releaseFunctionPointer = BurstCompiler.CompileFunctionPointer<ReleaseDelegate>(Test);

            HB.hb_draw_funcs_set_move_to_func(ptr, moveToFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_draw_funcs_set_line_to_func(ptr, lineToFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_draw_funcs_set_quadratic_to_func(ptr, quadraticToFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_draw_funcs_set_cubic_to_func(ptr, cubicToFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_draw_funcs_set_close_path_func(ptr, closeFunctionPointer, IntPtr.Zero, releaseFunctionPointer);

            HB.hb_draw_funcs_make_immutable(ptr);
        }

        public void Dispose()
        {
            HB.hb_draw_funcs_destroy(ptr);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(ReleaseDelegate))]
        public static void Test()
        {
            //Debug.Log($"harfbuzz blob called this delegate upon destroying blob ");
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(CloseDelegate))]
        public static void HB_draw_close_path_func_t(IntPtr dfuncs, ref DrawData data, ref DrawState st, IntPtr user_data)
        {
            data.contourIDs.Add(data.edges.Length);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(MoveToDelegate))]
        public static void HB_draw_move_to_func_t(IntPtr dfuncs, ref DrawData data, ref DrawState st, float to_x, float to_y, IntPtr user_data)
        {
			//data.contourIDs.Add(data.edges.Length);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(MoveToDelegate))]
        public static void HB_draw_line_to_func_t(IntPtr dfuncs, ref DrawData data, ref DrawState st, float to_x, float to_y, IntPtr user_data)
        {
            var edge = new SDFEdge
            {
                start_pos = new float2(st.current_x, st.current_y),
                end_pos = new float2(to_x, to_y),
                control1 = default,
                control2 = default,
                edge_type = SDFEdgeType.LINE,
            };
            var edgeBBox = BBox.GetLineBBox(edge.start_pos, edge.end_pos);

            data.edges.Add(edge);
            data.glyphRect = BBox.Union(data.glyphRect, edgeBBox);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(QuadraticToDelegate))]
        public static void HB_draw_quadratic_to_func_t(IntPtr dfuncs, ref DrawData data, ref DrawState st, float control_x, float control_y, float to_x, float to_y, IntPtr user_data)
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
            };
            var edgeBBox = BBox.GetQuadraticBezierBBox(edge.start_pos, edge.control1, edge.end_pos);

            data.edges.Add(edge);
            data.glyphRect = BBox.Union(data.glyphRect, edgeBBox);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(CubicToDelegate))]
        public static void HB_draw_cubic_to_func_t(IntPtr dfuncs, ref DrawData data, ref DrawState st, float control1_x, float control1_y, float control2_x, float control2_y, float to_x, float to_y, IntPtr user_data)
        {
            var edge = new SDFEdge
            {
                start_pos = new float2(st.current_x, st.current_y),
                end_pos = new float2(to_x, to_y),
                control1 = new float2(control1_x, control1_y),
                control2 = new float2(control2_x, control2_y),
                edge_type = SDFEdgeType.CUBIC,
            };
            var edgeBBox = BBox.GetCubicBezierBBox(edge.start_pos, edge.control1, edge.control1, edge.end_pos);

            data.edges.Add(edge);
            data.glyphRect = BBox.Union(data.glyphRect, edgeBBox);
        }


        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReleaseDelegate();

        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]        
        public delegate void MoveToDelegate(IntPtr dfuncs, ref DrawData data, ref DrawState st, float to_x, float to_y, IntPtr user_data);

        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void QuadraticToDelegate(IntPtr dfuncs, ref DrawData data, ref DrawState st, float control_x, float control_y, float to_x, float to_y, IntPtr user_data);

        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CubicToDelegate(IntPtr dfuncs, ref DrawData data, ref DrawState st, float control1_x, float control1_y, float control2_x, float control2_y, float to_x, float to_y, IntPtr user_data);
        
        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CloseDelegate(IntPtr dfuncs, ref DrawData data, ref DrawState st, IntPtr user_data);
    }
}
