using HarfBuzz.SDF;
using HarfBuzz;
using System.Runtime.InteropServices;
using System;
using TextMeshDOTS;
using UnityEngine;
using AOT;
using static HarfBuzz.SDF.DrawDelegates;

namespace HarfBuzz.SDF
{
    public struct PaintDelegates : IDisposable
    {
        public IntPtr ptr;
        public void Dispose()
        {
            HB.hb_paint_funcs_destroy(ptr);
        }
        public PaintDelegates(bool dummyProperty)
        {
            ptr = HB.hb_paint_funcs_create();
            var pushTransformDelegate = (PushTransformDelegate)HB_paint_push_transform_func_t;
            var popTransformDelegate = (PopDelegate)HB_paint_pop_transform_func_t;
            var colorGlyphDelegate = (ColorGlyphDelegate)hb_paint_color_glyph_func_t;
            var pushClipGlyphDelegate = (PushClipGlyphDelegate)HB_paint_push_clip_glyph_func_t;
            var pushClipRectangleDelegate = (PushClipRectangleDelegate)HB_paint_push_clip_rectangle_func_t;
            var popClipDelegate = (PopDelegate)HB_paint_pop_clip_func_t;
            var colorDelegate = (ColorDelegate)HB_paint_color_func_t;
            var imageDelegate = (ImageDelegate)hb_paint_image_func_t;
            var linearGradientDelegate = (GradientDelegate)HB_paint_linear_gradient_func_t;
            var radialGradientDelegate = (GradientDelegate)HB_paint_radial_gradient_func_t;
            var sweepGradientDelegate = (SweepGradientDelegate)HB_paint_sweep_gradient_func_t;
            var pushGroupDelegate = (PopDelegate)HB_paint_push_group_func_t;
            var popGroupDelegate = (PopGroupDelegate)HB_paint_pop_group_func_t;
            var customPaletteColorDelegate = (CustomPalette_colorDelegate)hb_paint_custom_palette_color_func_t;
            var releaseDelegate = (ReleaseDelegate)null;

            HB.hb_paint_funcs_set_push_transform_func(ptr, pushTransformDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_pop_transform_func(ptr, popTransformDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_color_glyph_func(ptr, colorGlyphDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_push_clip_glyph_func(ptr, pushClipGlyphDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_push_clip_rectangle_func(ptr, pushClipRectangleDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_pop_clip_func(ptr, popClipDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_color_func(ptr, colorDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_image_func(ptr, imageDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_linear_gradient_func(ptr, linearGradientDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_radial_gradient_func(ptr, radialGradientDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_sweep_gradient_func(ptr, sweepGradientDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_push_group_func(ptr, pushGroupDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_pop_group_func(ptr, popGroupDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_set_custom_palette_color_func(ptr, customPaletteColorDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_make_immutable(ptr);
        }

        public static void HB_paint_push_transform_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, float xx, float yx, float xy, float yy, float dx, float dy, IntPtr user_data)
        {
            Debug.Log("push_transform");
        }

        public static void HB_paint_pop_transform_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            Debug.Log("pop_transform");
        }

        public static bool hb_paint_color_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data)
        {
            Debug.Log("hb_paint_color_glyph");
            return true;
        }

        public static void HB_paint_push_clip_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data)
        {
            Debug.Log("push_clip_glyph");
        }

        public static void HB_paint_push_clip_rectangle_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, float xmin, float ymin, float xmax, float ymax, IntPtr user_data)
        {
            Debug.Log("push_clip_rectangle");
        }

        public static void HB_paint_pop_clip_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            Debug.Log("pop_clip");
        }

        public static void HB_paint_color_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, bool is_foreground, uint color, IntPtr user_data)
        {
            Debug.Log("color");
        }

        public static bool hb_paint_image_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_blob_t*/ Blob image, uint width, uint height, HB_PAINT_IMAGE_FORMAT format, float slant, ref GlyphExtents extents, IntPtr user_data)
        {            
            Debug.Log("hb_paint_image");
            data.imageBlob = image;
            data.imageFormat = format;
            Debug.Log($"width {width} height {height}  format {format} {image.Length}");
            return true;
        }

        public static void HB_paint_linear_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_color_line_t*/ IntPtr color_line, float x0, float y0, float x1, float y1, float x2, float y2, IntPtr user_data)
        {
            Debug.Log("linear_gradient");
        }

        public static void HB_paint_radial_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_color_line_t*/ IntPtr color_line, float x0, float y0, float r0, float x1, float y1, float r1, IntPtr user_data)
        {
            Debug.Log("radial_gradient");
        }

        public static void HB_paint_sweep_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_color_line_t*/ IntPtr color_line, float x0, float y0, float start_angle, float end_angle, IntPtr user_data)
        {
            Debug.Log("sweep_gradient");
        }


        public static void HB_paint_push_group_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            Debug.Log("push_group");
        }


        public static void HB_paint_pop_group_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, HB_PAINT_COMPOSITE_MODE mode, IntPtr user_data)
        {
            Debug.Log("pop_group");
        }

        public static bool hb_paint_custom_palette_color_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, uint color_index, uint color, IntPtr user_data)
        {
            Debug.Log("hb_paint_custom_palette_color");
            return true;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PushTransformDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, float xx, float yx, float xy, float yy, float dx, float dy, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PopDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool ColorGlyphDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PushClipGlyphDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PushClipRectangleDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, float xmin, float ymin, float xmax, float ymax, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ColorDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, bool is_foreground, uint color, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool ImageDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_blob_t*/ Blob image, uint width, uint height, HB_PAINT_IMAGE_FORMAT format, float slant, ref GlyphExtents extents, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void GradientDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_color_line_t*/ IntPtr color_line, float x0, float y0, float x1, float y1, float x2, float y2, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SweepGradientDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_color_line_t*/ IntPtr color_line, float x0, float y0, float start_angle, float end_angle, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PopGroupDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, HB_PAINT_COMPOSITE_MODE mode, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool CustomPalette_colorDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, uint color_index, uint color, IntPtr user_data);

    }
    public enum HB_PAINT_IMAGE_FORMAT
    {
        PNG = ('p' << 24) | ('n' << 16) | ('g' << 8) | ' ', //better would be HB.HB_TAG('c', 'p', 'c', 't'), but this does not work in C Sharp,
        SVG = ('s' << 24) | ('v' << 16) | ('g' << 8) | ' ',
        BGRA = ('B' << 24) | ('R' << 16) | ('G' << 8) | 'A',

    }
    public enum HB_PAINT_COMPOSITE_MODE
    {
        CLEAR,
        SRC,
        DEST,
        SRC_OVER,
        DEST_OVER,
        SRC_IN,
        DEST_IN,
        SRC_OUT,
        DEST_OUT,
        SRC_ATOP,
        DEST_ATOP,
        XOR,
        PLUS,
        SCREEN,
        OVERLAY,
        DARKEN,
        LIGHTEN,
        COLOR_DODGE,
        COLOR_BURN,
        HARD_LIGHT,
        SOFT_LIGHT,
        DIFFERENCE,
        EXCLUSION,
        MULTIPLY,
        HSL_HUE,
        HSL_SATURATION,
        HSL_COLOR,
        HSL_LUMINOSITY
    }
}
