using System.Runtime.InteropServices;
using System;
using UnityEngine;
using Unity.Mathematics;
using static TextMeshDOTS.HarfBuzz.SDF.DrawDelegates;

namespace TextMeshDOTS.HarfBuzz.SDF
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
            //HB.hb_paint_funcs_set_custom_palette_color_func(ptr, customPaletteColorDelegate, IntPtr.Zero, releaseDelegate);
            HB.hb_paint_funcs_make_immutable(ptr);
        }

        public static void HB_paint_push_transform_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, float xx, float yx, float xy, float yy, float dx, float dy, IntPtr user_data)
        {
            //var oldTransformM2 = data.transformStack.Peek();
            //var oldTransform = new float3x3(
            //    new float3(oldTransformM2.c0, 0),
            //    new float3(oldTransformM2.c1, 0),
            //    new float3(0, 0, 1));
            //var newTransform = new float3x3(
            //    new float3(xx, yx, 0),
            //    new float3(xy, yy, 0),
            //    new float3(0, 0, 1));
            //var combined = math.mul(oldTransform, newTransform);
            //var final = new float2x3
            //{
            //    c0 = combined.c0.xy,
            //    c1 = combined.c1.xy,
            //    c2 = new float2
            //    {
            //        x = oldTransformM2.c2.x - dx,
            //        y = oldTransformM2.c2.y - dy,
            //    }
            //};

            var final = new float2x3
            {
                c0 = new float2(xx, yx),
                c1 = new float2(xy, yy),
                c2 = new float2(-dx, -dy)
            };

            data.transformStack.Add(final);
            Debug.Log($"push_transform {xx} {yx} {xy} {yy} {dx} {dy}");
            //Debug.Log($"current transfrom xx {final.c0.x} yx {final.c0.y} xy {final.c1.x} yy {final.c1.y} dx {final.c2.x} {final.c2.y}");
        }

        public static void HB_paint_pop_transform_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            data.transformStack.Pop();
            Debug.Log("pop_transform");
        }

        public static bool hb_paint_color_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data)
        {
            Debug.Log("hb_paint_color_glyph");
            return true;
        }

        public static void HB_paint_push_clip_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data)
        {
            HB.hb_font_draw_glyph(font, glyph, data.drawDelegates, ref data.clipGlyph);
            //SDFCommon.WriteGlyphOutlineToFile("ClipGlyph.txt", ref data.clipGlyph);
            Debug.Log($"push_clip_glyph {glyph}");
        }

        public static void HB_paint_push_clip_rectangle_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, float xmin, float ymin, float xmax, float ymax, IntPtr user_data)
        {
            data.clipRect = new BBox(new float2(xmin, ymin), new float2(xmax, ymax));
            Debug.Log($"push_clip_rectangle {data.clipRect}");
        }

        public static void HB_paint_pop_clip_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            data.clipGlyph.Clear();
            data.clipRect = BBox.Empty;
            Debug.Log("pop_clip");
        }

        public static void HB_paint_color_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, bool is_foreground, uint color, IntPtr user_data)
        {
            //color = data.color;
            //SDFCommon.CenterGlyphInGlyphRect(ref data.clipGlyph, (int)data.clipGlyph.glyphRect.width, (int)data.clipGlyph.glyphRect.width, 0);
            //SDFCommon.TransformGlyph(ref data.clipGlyph, data.transformStack.Peek());

            for (int i = 0, ii=data.transformStack.m_buffer.Length; i < ii; i++)
            //for (int i = data.transformStack.m_buffer.Length-1; i >=0 ; i--)
            {
                var transform = data.transformStack.m_buffer[i];
                SDFCommon.TransformGlyph(ref data.clipGlyph, transform);
            }

            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.textureData, color, data.width, data.height);
            //Debug.Log($"color {color}");            
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
            //Debug.Log($"hb_paint_custom_palette_color color_index {color_index} color {color}");
            data.color = color;
            var r = HB.hb_color_get_red(color);
            var g = HB.hb_color_get_green(color);
            var b = HB.hb_color_get_blue(color);
            var a = HB.hb_color_get_alpha(color);
            Color32 textureColor = new Color32(r, g, b, a);
            Debug.Log($"hb_paint_custom_palette_color color_index {color_index} color {textureColor}");
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
