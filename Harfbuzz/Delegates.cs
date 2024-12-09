using System;
using Unity.Mathematics;

namespace HarfBuzz.SDF
{
    public delegate void ReleaseDelegate();
    public delegate void MoveToDelegate(IntPtr dfuncs, ref BezierData data, ref DrawState st, float to_x, float to_y, IntPtr user_data);
    public delegate void QuadraticToDelegate(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control_x, float control_y, float to_x, float to_y, IntPtr user_data);
    public delegate void CubicToDelegate(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control1_x, float control1_y, float control2_x, float control2_y, float to_x, float to_y, IntPtr user_data);
    public static unsafe class HBDelegateProxies
    {
        public static void Test()
        {
            //Debug.Log($"harfbuzz blob called this delegate upon destroying blob ");
        }
        public static void hb_draw_extents_move_to(IntPtr dfuncs, ref BezierData data, ref DrawState st, float to_x, float to_y, IntPtr user_data )
        {
            //Debug.Log($"Move to {to_x} {to_y}");
            //Debug.Log($"Open? {st.path_open} {st.path_start_x} {st.path_start_y} {st.current_x} {st.current_y}");
        }

        public static void hb_draw_extents_line_to(IntPtr dfuncs, ref BezierData data, ref DrawState st, float to_x, float to_y, IntPtr user_data)
        {
            //Debug.Log($"Point to {to_x} {to_y} {st}");
            data.edges.Add(new SDFEdge
            {
                start_pos = new float2(st.current_x, st.current_y),
                end_pos = new float2(to_x, to_y),
                control1 = default,
                control2 = default,
                edge_type = SDFEdgeType.LINE,
            });
        }

        public static void hb_draw_extents_quadratic_to(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control_x, float control_y, float to_x, float to_y, IntPtr user_data)
        {
            //Debug.Log($"Quadratic add control {control_x} {control_y}");
            //Debug.Log($"Quadratic add point {to_x} {to_y}");
            data.edges.Add(new SDFEdge
            {
                start_pos = new float2(st.current_x, st.current_y),
                end_pos = new float2(to_x, to_y),
                control1 = new float2(control_x, control_y),
                control2 = default,
                edge_type = SDFEdgeType.CONIC,
            });
        }

        public static void hb_draw_extents_cubic_to(IntPtr dfuncs, ref BezierData data, ref DrawState st, float control1_x, float control1_y, float control2_x, float control2_y, float to_x, float to_y, IntPtr user_data)
        {
            //Debug.Log($"Cubic add control 1 {control1_x} {control1_y}");
            //Debug.Log($"Cubic add control 2 {control2_x} {control2_x}");
            //Debug.Log($"Cubic add point {to_x} {to_y}");
            //Debug.Log($"Open? {st.path_open} {st.path_start_x} {st.path_start_y} {st.current_x} {st.current_y}");
            data.edges.Add(new SDFEdge { 
                start_pos = new float2(st.current_x,st.current_y),
                end_pos =  new float2(to_x, to_y),
                control1 = new float2(control1_x, control1_y),
                control2 = new float2(control2_x, control2_y),
                edge_type = SDFEdgeType.CUBIC,
            });
        }


        //static void hb_draw_extents_move_to(hb_draw_funcs_t* dfuncs HB_UNUSED, void* data, hb_draw_state_t* st, float to_x, float to_y, void* user_data HB_UNUSED)
        //{
        //    hb_extents_t* extents = (hb_extents_t*)data;

        //    extents->add_point(to_x, to_y);
        //}

        //static void hb_draw_extents_line_to(hb_draw_funcs_t* dfuncs HB_UNUSED, void* data, hb_draw_state_t* st, float to_x, float to_y, void* user_data HB_UNUSED)
        //{
        //    hb_extents_t* extents = (hb_extents_t*)data;

        //    extents->add_point(to_x, to_y);
        //}

        //static void hb_draw_extents_quadratic_to(hb_draw_funcs_t* dfuncs HB_UNUSED, void* data, hb_draw_state_t* st, float control_x, float control_y, float to_x, float to_y, void* user_data HB_UNUSED)
        //{
        //    hb_extents_t* extents = (hb_extents_t*)data;

        //    extents->add_point(control_x, control_y);
        //    extents->add_point(to_x, to_y);
        //}

        //static void hb_draw_extents_cubic_to(hb_draw_funcs_t* dfuncs HB_UNUSED, void* data, hb_draw_state_t* st, float control1_x, float control1_y, float control2_x, float control2_y, float to_x, float to_y, void* user_data HB_UNUSED)
        //{
        //    hb_extents_t* extents = (hb_extents_t*)data;

        //    extents->add_point(control1_x, control1_y);
        //    extents->add_point(control2_x, control2_y);
        //    extents->add_point(to_x, to_y);
        //}
    }
}
