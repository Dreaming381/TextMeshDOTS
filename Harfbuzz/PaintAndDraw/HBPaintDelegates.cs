using System.Runtime.InteropServices;
using System;
using UnityEngine;
using Unity.Mathematics;
using static TextMeshDOTS.HarfBuzz.SDF.DrawDelegates;
using Unity.Collections;

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
            var linearGradientDelegate = (LinearOrRadialGradientDelegate)HB_paint_linear_gradient_func_t;
            var radialGradientDelegate = (LinearOrRadialGradientDelegate)HB_paint_radial_gradient_func_t;
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
            var transform = new float2x3
            {
                c0 = new float2(xx, yx),
                c1 = new float2(xy, yy),
                c2 = new float2(dx, dy)
            };
            //transform = PaintUtils.mul(data.transformStack.Peek(), transform);
            transform = PaintUtils.mul(transform, data.transformStack.Peek());
            data.transformStack.Add(transform);
            //Debug.Log($"push transform {xx} {yx} {xy} {yy} {dx} {dy} (stack: {data.transformStack.Length}) ");
        }        

        public static void HB_paint_pop_transform_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {            
            data.transformStack.Pop();
            //Debug.Log($"pop transform (stack: {data.transformStack.Length})");
        }

        public static bool hb_paint_color_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data)
        {
            //Debug.Log("hb_paint_color_glyph");
            return true;
        }

        public static void HB_paint_push_clip_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data)
        {
            HB.hb_font_draw_glyph(font, glyph, data.drawDelegates, ref data.clipGlyph);
            //SDFCommon.WriteGlyphOutlineToFile("ClipGlyph.txt", ref data.clipGlyph, false);
            //Debug.Log($"push clip glyph {glyph}; clipGlyph Rect: {data.clipGlyph.glyphRect}");
        }

        public static void HB_paint_push_clip_rectangle_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, float xmin, float ymin, float xmax, float ymax, IntPtr user_data)
        {
            var clipRect = new BBox(new float2(xmin, ymin), new float2(xmax, ymax));
            data.clipRect = clipRect;
            data.textureData = new NativeArray<ColorARGB>((int)(clipRect.width) * (int)clipRect.height, Allocator.Temp);
            //data.textureData = new NativeArray<ColorARGB>(1024 * 1024, Allocator.Temp);
            //Debug.Log($"push clip rectangle {clipRect}");
        }

        public static void HB_paint_pop_clip_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            data.clipGlyph.Clear();
            //Debug.Log("pop clip glyph");
        }

        public static void HB_paint_color_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, bool is_foreground, ColorARGB color, IntPtr user_data)
        {
            //Debug.Log($"color {color}");
            var solidColor = new SolidColor(color);

            var transform = data.transformStack.Peek();
            PaintUtils.TransformGlyph(ref data.clipGlyph, transform);
            
            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.textureData, solidColor, data.clipRect);            
        }       
        public static void HB_paint_linear_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine colorLine, float x0, float y0, float x1, float y1, float x2, float y2, IntPtr user_data)
        {
            //Debug.Log($"Line gradient: {x0} {y0} / {x1} {y1} / {x2} {y2}");
            var lineGradient = new LineGradient(x0, y0, x1, y1, x2, y2, colorLine.GetExtend());
            if (!lineGradient.isValid)
            {
                Debug.LogError("Line gradient is not valid");
                return;
            }
            lineGradient.InitializeColorLine(colorLine);

            var transform = data.transformStack.Peek();
            PaintUtils.TransformGlyph(ref data.clipGlyph, transform);            

            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.textureData, lineGradient, data.clipRect);
        }

        public static void HB_paint_radial_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine colorLine, float x0, float y0, float r0, float x1, float y1, float r1, IntPtr user_data)
        {
            //Debug.Log($"Radial gradient: {x0} {y0} {r0} / {x1} {y1} {r1}");
            var radialGradient = new RadialGradient(x0, y0, r0, x1, y1, r1, colorLine.GetExtend());
            if (!radialGradient.isValid)
            {
                Debug.LogError("Radial gradient is not valid");
                return;
            }
            radialGradient.InitializeColorLine(colorLine);

            var transform = data.transformStack.Peek();
            PaintUtils.TransformGlyph(ref data.clipGlyph, transform);
            
            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.textureData, radialGradient, data.clipRect);
        }

        public static void HB_paint_sweep_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine colorLine, float x0, float y0, float startAngle, float endAngle, IntPtr user_data)
        {
            //Debug.Log($"Sweep gradient {x0} {y0} {math.degrees(startAngle)} {math.degrees(endAngle)}");
            var sweepGradient = new SweepGradient(x0, y0, startAngle, endAngle, colorLine.GetExtend());
            sweepGradient.InitializeColorLine(colorLine);
            if (!sweepGradient.isValid)
            {
                Debug.LogError("Sweep gradient is not valid");
                return;
            }

            var transform = data.transformStack.Peek();
            PaintUtils.TransformGlyph(ref data.clipGlyph, transform);
            
            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.textureData, sweepGradient, data.clipRect);
        }

        public static void HB_paint_push_group_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            //Debug.Log("push group");
        }

        public static void HB_paint_pop_group_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, HB_PAINT_COMPOSITE_MODE mode, IntPtr user_data)
        {
            //Debug.Log("pop group");
        }

        public static bool hb_paint_custom_palette_color_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, uint color_index, ColorARGB color, IntPtr user_data)
        {
            //Debug.Log($"hb_paint_custom_palette_color color_index {color_index} color {color}");
            return true;
        }

        public static bool hb_paint_image_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_blob_t*/ Blob image, uint width, uint height, HB_PAINT_IMAGE_FORMAT format, float slant, ref GlyphExtents extents, IntPtr user_data)
        {
            Debug.Log("hb_paint_image");
            data.imageBlob = image;
            data.imageFormat = format;
            Debug.Log($"width {width} height {height}  format {format} {image.Length}");
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
        public delegate void ColorDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, bool is_foreground, ColorARGB color, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool ImageDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_blob_t*/ Blob image, uint width, uint height, HB_PAINT_IMAGE_FORMAT format, float slant, ref GlyphExtents extents, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LinearOrRadialGradientDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine color_line, float x0, float y0, float x1, float y1, float x2, float y2, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SweepGradientDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine color_line, float x0, float y0, float start_angle, float end_angle, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PopGroupDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, HB_PAINT_COMPOSITE_MODE mode, IntPtr user_data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool CustomPalette_colorDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, uint color_index, ColorARGB color, IntPtr user_data);

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
