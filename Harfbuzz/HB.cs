using System;
using System.Runtime.InteropServices;

namespace HarfBuzz
{

    public static unsafe class HB
    {
        public static uint HB_TAG(char c1, char c2, char c3, char c4)
        {
            return (((uint)c1 & 0xFF) << 24) | (((uint)c2 & 0xFF) << 16) | (((uint)c3 & 0xFF) << 8) | ((uint)c4 & 0xFF);
        }

        private const string HarfBuzz = "libharfbuzz-0.dll";
        private const CallingConvention CallConvention = CallingConvention.Cdecl;

        #region blob
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]        
        internal static extern bool hb_blob_is_immutable(IntPtr blob);

        
        [DllImport(HarfBuzz, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr hb_blob_create(void* data, uint length, MemoryMode mode, void* user_data, ReleaseDelegate destroy);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern IntPtr hb_blob_create_from_file([MarshalAs(UnmanagedType.LPStr)] string file_name);
        //internal static extern IntPtr hb_blob_create_from_file(byte* file_name);//do not use. big risk of not passing  NULL terminated char*


        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_blob_destroy(IntPtr blob);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern uint hb_blob_get_length(IntPtr blob);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern uint hb_face_count(IntPtr blob);
        #endregion

        #region face
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool hb_face_is_immutable(IntPtr face);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool hb_ot_layout_get_size_params(IntPtr face, out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern uint hb_face_get_upem(IntPtr face);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_face_set_upem(IntPtr face, uint upem);


        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern IntPtr hb_face_create(IntPtr blob, UInt32 index);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_face_destroy(IntPtr face);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern bool hb_ot_var_has_data(IntPtr face);
        #endregion


        #region font
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_make_immutable(IntPtr font);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool hb_font_is_immutable(IntPtr font);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool hb_ot_layout_get_baseline(IntPtr font, OpenTypeLayoutBaselineTag baseline_tag, Direction direction, Script script_tag, uint language_tag, out int coord);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_get_glyph_advance_for_direction(IntPtr font, uint glyph, Direction direction, out int x, out int y);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_get_extents_for_direction(IntPtr font, Direction direction, out FontExtents extents);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool hb_font_get_glyph_extents(IntPtr font, uint glyph, out GlyphExtents extents);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool hb_ot_metrics_get_position(IntPtr font, MetricTag metrics_tag, out int position);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern IntPtr hb_font_create(IntPtr face);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_destroy(IntPtr font);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern float hb_style_get_value(IntPtr font, StyleTag style_tag);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_get_ppem(IntPtr font, out uint x_ppem, out uint y_ppem);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_set_ppem(IntPtr font, uint x_ppem, uint y_ppem);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern float hb_font_get_ptem(IntPtr font);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_set_ptem(IntPtr font, float ptem);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_get_scale(IntPtr font,out int x_scale,out int y_scale);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_font_set_scale(IntPtr font, int x_scale, int y_scale);
        #endregion


        #region buffer

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_set_segment_properties(IntPtr buffer, SegmentProperties *props);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_get_segment_properties(IntPtr buffer, SegmentProperties *props);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_clear_contents(IntPtr buffer);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_reset(IntPtr buffer);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern ClusterLevel hb_buffer_get_cluster_level(IntPtr buffer);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_set_cluster_level(IntPtr buffer, ClusterLevel cluster_level);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_add(IntPtr buffer, UInt32 codepoint, UInt32 cluster);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_add_utf8(IntPtr buffer, byte* text, int text_length, uint item_offset, int item_length);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern IntPtr hb_buffer_create();

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_destroy(IntPtr buffer);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        //internal static extern IntPtr hb_buffer_get_glyph_infos(IntPtr buffer, out uint length);
        internal static extern GlyphInfo* hb_buffer_get_glyph_infos(IntPtr buffer, out uint length);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        //internal static extern IntPtr hb_buffer_get_glyph_positions(IntPtr buffer, out uint length);
        internal static extern  GlyphPosition* hb_buffer_get_glyph_positions(IntPtr buffer, out uint length);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        public static extern IntPtr hb_language_from_string([MarshalAs(UnmanagedType.LPStr)] string str, int len);
        /// <summary> DANGER: ensure str is NULL terminated UTF8 when using -1 as length </summary>
        //public static extern IntPtr hb_language_from_string(byte* str, int len);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        /// <summary> DANGER: convert value is null terminated UTF8 </summary>
        public static extern IntPtr hb_language_to_string(IntPtr language);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern Language hb_buffer_get_language(IntPtr buffer);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_set_language(IntPtr buffer, Language language);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern uint hb_buffer_get_length(IntPtr buffer);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern ContentType hb_buffer_get_content_type(IntPtr buffer);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_set_content_type(IntPtr buffer, ContentType content_type);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern Direction hb_buffer_get_direction(IntPtr buffer);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_set_direction(IntPtr buffer, Direction direction);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern Script hb_buffer_get_script(IntPtr buffer);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_set_script(IntPtr buffer, Script script);
        #endregion

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern uint hb_face_get_glyph_count(IntPtr face);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool hb_feature_from_string([MarshalAs(UnmanagedType.LPStr)] string str, int len, out Feature feature);
        //internal static extern bool hb_feature_from_string(byte* str, Int32 len, out Feature feature);
        
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_feature_to_string(Feature* feature, out byte str, uint size);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        public static extern void hb_shape(IntPtr font, IntPtr buffer, IntPtr features, uint num_features);
        //[DllImport(HarfBuzz, CallingConvention = CallConvention)]
        //internal static extern void hb_shape(IntPtr font, IntPtr buffer, Feature* features, uint num_features);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern IntPtr hb_ot_tag_to_language(uint tag);

    }
}
