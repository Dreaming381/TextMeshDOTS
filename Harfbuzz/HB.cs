using System;
using System.Runtime.InteropServices;
using UnityEngine;

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
        internal static extern IntPtr hb_blob_create_from_file(byte* file_name);
        //internal static extern IntPtr hb_blob_create_from_file([MarshalAs(UnmanagedType.LPStr)] string file_name);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_blob_destroy(IntPtr blob);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern uint hb_blob_get_length(IntPtr blob);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern uint hb_face_count(IntPtr blob);
        #endregion

        #region face

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern IntPtr hb_face_create(IntPtr blob, UInt32 index);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_face_destroy(IntPtr face);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern bool hb_ot_var_has_data(IntPtr face);
        #endregion


        #region font
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
        //public static extern IntPtr hb_language_from_string(string str, int len);
        public static extern IntPtr hb_language_from_string(byte* str, int len);
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        public static extern /* char */ void* hb_language_to_string(IntPtr language);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern IntPtr hb_buffer_get_language(IntPtr buffer);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_buffer_set_language(IntPtr buffer, IntPtr language);

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
        //internal static extern bool hb_feature_from_string([MarshalAs(UnmanagedType.LPStr)] String str, Int32 len, out Feature);
        internal static extern bool hb_feature_from_string(byte* str, Int32 len, out Feature feature);
        
        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        internal static extern void hb_feature_to_string(Feature* feature, /* char */ void* buf, UInt32 size);

        [DllImport(HarfBuzz, CallingConvention = CallConvention)]
        public static extern void hb_shape(IntPtr font, IntPtr buffer, IntPtr features, uint num_features);
        //[DllImport(HarfBuzz, CallingConvention = CallConvention)]
        //internal static extern void hb_shape(IntPtr font, IntPtr buffer, Feature* features, uint num_features);

    }
}
