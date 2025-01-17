using System.Runtime.InteropServices;
using System;
using UnityEngine;
using Unity.Mathematics;
using static TextMeshDOTS.HarfBuzz.SDF.DrawDelegates;
using Unity.Collections;
using Unity.Burst;
using AOT;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    [BurstCompile]
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
            FunctionPointer<PushTransformDelegate> pushTransformFunctionPointer = BurstCompiler.CompileFunctionPointer<PushTransformDelegate>(HB_paint_push_transform_func_t);
            FunctionPointer<PopDelegate> popTransformFunctionPointer = BurstCompiler.CompileFunctionPointer<PopDelegate>(HB_paint_pop_transform_func_t);
            FunctionPointer<ColorGlyphDelegate> colorGlyphFunctionPointer = BurstCompiler.CompileFunctionPointer<ColorGlyphDelegate>(hb_paint_color_glyph_func_t);
            FunctionPointer<PushClipGlyphDelegate> pushClipGlyphFunctionPointer = BurstCompiler.CompileFunctionPointer<PushClipGlyphDelegate>(HB_paint_push_clip_glyph_func_t);
            FunctionPointer<PushClipRectangleDelegate> pushClipRectangleFunctionPointer = BurstCompiler.CompileFunctionPointer<PushClipRectangleDelegate>(HB_paint_push_clip_rectangle_func_t);
            FunctionPointer<PopDelegate> popClipFunctionPointer = BurstCompiler.CompileFunctionPointer<PopDelegate>(HB_paint_pop_clip_func_t);
            FunctionPointer<ColorDelegate> colorFunctionPointer = BurstCompiler.CompileFunctionPointer<ColorDelegate>(HB_paint_color_func_t);
            FunctionPointer<LinearOrRadialGradientDelegate> linearGradientFunctionPointer = BurstCompiler.CompileFunctionPointer<LinearOrRadialGradientDelegate>(HB_paint_linear_gradient_func_t);
            FunctionPointer<LinearOrRadialGradientDelegate> radialGradientFunctionPointer = BurstCompiler.CompileFunctionPointer<LinearOrRadialGradientDelegate>(HB_paint_radial_gradient_func_t);
            FunctionPointer<SweepGradientDelegate> sweepGradientFunctionPointer = BurstCompiler.CompileFunctionPointer<SweepGradientDelegate>(HB_paint_sweep_gradient_func_t);
            FunctionPointer<PopDelegate> pushGroupFunctionPointer = BurstCompiler.CompileFunctionPointer<PopDelegate>(HB_paint_push_group_func_t);
            FunctionPointer<PopGroupDelegate> popGroupFunctionPointer = BurstCompiler.CompileFunctionPointer<PopGroupDelegate>(HB_paint_pop_group_func_t);
            FunctionPointer<CustomPalette_colorDelegate> customPaletteColorFunctionPointer = BurstCompiler.CompileFunctionPointer<CustomPalette_colorDelegate>(hb_paint_custom_palette_color_func_t);
            FunctionPointer<ImageDelegate> imageFunctionPointer = BurstCompiler.CompileFunctionPointer<ImageDelegate>(hb_paint_image_func_t);
            FunctionPointer<ReleaseDelegate> releaseFunctionPointer = BurstCompiler.CompileFunctionPointer<ReleaseDelegate>(Test);

            HB.hb_paint_funcs_set_push_transform_func(ptr, pushTransformFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_pop_transform_func(ptr, popTransformFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_color_glyph_func(ptr, colorGlyphFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_push_clip_glyph_func(ptr, pushClipGlyphFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_push_clip_rectangle_func(ptr, pushClipRectangleFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_pop_clip_func(ptr, popClipFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_color_func(ptr, colorFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_linear_gradient_func(ptr, linearGradientFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_radial_gradient_func(ptr, radialGradientFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_sweep_gradient_func(ptr, sweepGradientFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_push_group_func(ptr, pushGroupFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_pop_group_func(ptr, popGroupFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            //HB.hb_paint_funcs_set_custom_palette_color_func(ptr, customPaletteColorFunctionPointer, IntPtr.Zero, releaseFunctionPointer);
            HB.hb_paint_funcs_set_image_func(ptr, imageFunctionPointer, IntPtr.Zero, releaseFunctionPointer);

            HB.hb_paint_funcs_make_immutable(ptr);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(PushTransformDelegate))]
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
        [BurstCompile]
        [MonoPInvokeCallback(typeof(PopDelegate))]
        public static void HB_paint_pop_transform_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {            
            data.transformStack.Pop();
            //Debug.Log($"pop transform (stack: {data.transformStack.Length})");
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(ColorGlyphDelegate))]
        public static bool hb_paint_color_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, uint glyphID, IntPtr font, IntPtr user_data)
        {
            Debug.Log("hb_paint_color_glyph");
            return true;
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(PushClipGlyphDelegate))]
        public static void HB_paint_push_clip_glyph_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, uint glyphID, IntPtr font, IntPtr user_data)
        {
            data.glyphID = glyphID;
            HB.hb_font_draw_glyph(font, glyphID, data.drawDelegates, ref data.clipGlyph);
            //if (PaintUtils.filterGlyphs.Contains((int)glyphID))
            //    SDFCommon.WriteGlyphOutlineToFile($"ClipGlyph_{glyphID}.txt", ref data.clipGlyph, true);
            //Debug.Log($"push clip glyph {glyphID}; clipGlyph Rect: {data.clipGlyph.glyphRect}");
            PaintUtils.TransformGlyph(ref data.clipGlyph, data.transformStack.Peek());
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(PushClipRectangleDelegate))]
        public static void HB_paint_push_clip_rectangle_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, float xmin, float ymin, float xmax, float ymax, IntPtr user_data)
        {
            var clipRect = new BBox(new float2(xmin, ymin), new float2(xmax, ymax));
            data.clipRect = clipRect;
            data.finalTexture = new NativeArray<ColorARGB>((int)(clipRect.width) * (int)clipRect.height, Allocator.Temp);
            //for (int i = 0; i < data.finalTexture.Length; i++)
            //    data.finalTexture[i] = new ColorARGB(255, 255, 255, 255);
            //Debug.Log($"push clip rectangle {clipRect}");
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(PopDelegate))]
        public static void HB_paint_pop_clip_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            data.clipGlyph.Clear();
            //Debug.Log("pop clip glyph");
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(ColorDelegate))]
        public static void HB_paint_color_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, bool is_foreground, uint color, IntPtr user_data)
        {
            if (!PaintUtils.DrawGlyph((int)data.glyphID))
                return;
            Debug.Log($"color {(ColorARGB)color}");
            var solidColor = new SolidColor((ColorARGB)color);
            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.finalTexture, solidColor, data.clipRect);            
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(LinearOrRadialGradientDelegate))]
        public static void HB_paint_linear_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine colorLine, float x0, float y0, float x1, float y1, float x2, float y2, IntPtr user_data)
        {
            if (!PaintUtils.DrawGlyph((int)data.glyphID))
                return;
            Debug.Log($"Line gradient: {x0} {y0} / {x1} {y1} / {x2} {y2}"); 
            var lineGradient = new LineGradient(x0, y0, x1, y1, x2, y2, colorLine.GetExtend(), data.transformStack.Peek());
            if (!lineGradient.isValid)
            {
                Debug.LogError("Line gradient is not valid");
                return;
            }
            lineGradient.InitializeColorLine(colorLine);
            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.finalTexture, lineGradient, data.clipRect);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(LinearOrRadialGradientDelegate))]
        public static void HB_paint_radial_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine colorLine, float x0, float y0, float r0, float x1, float y1, float r1, IntPtr user_data)
        {
            if (!PaintUtils.DrawGlyph((int)data.glyphID))
                return;
            Debug.Log($"Radial gradient: {x0} {y0} {r0} / {x1} {y1} {r1}");            
            var radialGradient = new RadialGradient(x0, y0, r0, x1, y1, r1, colorLine.GetExtend(), data.transformStack.Peek());
            if (!radialGradient.isValid)
            {
                Debug.LogError("Radial gradient is not valid");
                return;
            }
            radialGradient.InitializeColorLine(colorLine);
            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.finalTexture, radialGradient, data.clipRect);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(SweepGradientDelegate))]
        public static void HB_paint_sweep_gradient_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine colorLine, float x0, float y0, float startAngle, float endAngle, IntPtr user_data)
        {
            if (!PaintUtils.DrawGlyph((int)data.glyphID))
                return;
            Debug.Log($"Sweep gradient {x0} {y0} {math.degrees(startAngle)} {math.degrees(endAngle)}");       
            var sweepGradient = new SweepGradient(x0, y0, startAngle, endAngle, colorLine.GetExtend(), data.transformStack.Peek());
            sweepGradient.InitializeColorLine(colorLine);
            if (!sweepGradient.isValid)
            {
                Debug.LogError("Sweep gradient is not valid");
                return;
            }
            ScanlineRasterizer.Rasterize(ref data.clipGlyph, data.finalTexture, sweepGradient, data.clipRect);
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(PopDelegate))]
        public static void HB_paint_push_group_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data)
        {
            //logic for pushing / poping groups explained here: https://github.com/harfbuzz/harfbuzz/issues/3931
            // COMPOSITE: 
            // push_group()
            // // recurse for backdrop
            // push_group()
            // // recurse for source
            // pop_group_and_composite(composite_mode)
            // pop_group_and_composite(OVER)

            // layers:
            //foreach layer
            //    push_group()
            //    // recurse for layer paint
            //    pop_group_and_composite(OVER)

            if (data.group == 0)
            {
                if (data.backDrop.IsCreated) data.backDrop.Dispose();                
                data.backDrop = data.finalTexture;
                data.finalTexture = new NativeArray<ColorARGB>(data.backDrop.Length, Allocator.Temp);                
                Debug.Log($"push backdrop group, new: {data.group + 1})");
            }
            else if (data.group == 1)
            {
                if (data.foreGround.IsCreated) data.foreGround.Dispose();
                data.foreGround = data.finalTexture;
                data.finalTexture = new NativeArray<ColorARGB>(data.foreGround.Length, Allocator.Temp);
                Debug.Log($"push foreground group, new: {data.group + 1})");
            }
            else if (data.group == 2)
            {
                Debug.Log($"push unexpected group, new: {data.group + 1})");
            }

            data.group++;            
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(PopGroupDelegate))]
        public static void HB_paint_pop_group_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, HB_PAINT_COMPOSITE_MODE mode, IntPtr user_data)
        {
            //logic for pushing / poping groups explained here: https://github.com/harfbuzz/harfbuzz/issues/3931
            NativeArray<ColorARGB> result;
            NativeArray<ColorARGB> source;
            NativeArray<ColorARGB> destination;
            if (data.group == 1)
            {
                result = data.finalTexture;
                source = data.finalTexture;
                destination = data.backDrop;
                Debug.Log($"pop group (new: {data.group - 1}), use {mode} to combine backdrop with final texture");
            }
            else if (data.group == 2)
            {
                result = data.backDrop;
                source = data.foreGround;
                destination = data.backDrop;
                Debug.Log($"pop group (new: {data.group - 1}), use {mode} to combine backdrop with foreground");
            }
            else
            {
                result = data.finalTexture;
                source = data.finalTexture;
                destination = data.finalTexture;
                Debug.Log("unexpected group");
            }

            switch (mode)
            {
                case HB_PAINT_COMPOSITE_MODE.SRC:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = source[i];
                    break;
                case HB_PAINT_COMPOSITE_MODE.DEST:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = destination[i];
                    break;
                case HB_PAINT_COMPOSITE_MODE.SRC_OVER:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.Normal(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.DEST_OVER:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.DstOver(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.SRC_IN:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.SrcIn(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.DEST_IN:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.DstIn(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.SRC_OUT:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.SrcOut(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.DEST_OUT:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.DstOut(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.SRC_ATOP:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.SrcAtop(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.DEST_ATOP:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.DstAtop(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.XOR:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.Xor(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.PLUS:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.Plus(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.SCREEN:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.Screen(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.MULTIPLY:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.Multiply(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.COLOR_DODGE:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.ColorDodge(source[i], destination[i]);
                    break;
                case HB_PAINT_COMPOSITE_MODE.COLOR_BURN:
                    for (int i = 0; i < result.Length; i++)
                        result[i] = Blending.ColorBurn(source[i], destination[i]);
                    break;
            }
            data.group--;
            
        }
        [BurstCompile]
        [MonoPInvokeCallback(typeof(CustomPalette_colorDelegate))]
        public static bool hb_paint_custom_palette_color_func_t (IntPtr harfBuzzPaintFunct, ref PaintData data, uint color_index, uint color, IntPtr user_data)
        {
            //Debug.Log($"hb_paint_custom_palette_color color_index {color_index} color {color}");
            return true;
        }

        /// <summary>
        /// This callback converts the image data found for a given glyph either to a NativeArray or colors that can be directly applied to a texture 
        /// (for Apple sbix, and Google CDBT), or to the raw PNG and SVG bytes. PNG can SVG can currently not be converted to color in a BURST compatible way.
        /// </summary>
        [BurstCompile]
        [MonoPInvokeCallback(typeof(ImageDelegate))]
        public static bool hb_paint_image_func_t(IntPtr harfBuzzPaintFunct, ref PaintData data, Blob image, uint width, uint height, HB_PAINT_IMAGE_FORMAT format, float slant, ref GlyphExtents extents, IntPtr user_data)
        {
            Debug.Log("hb_paint_image");
            data.imageFormat = format;            
            data.imageWidth = (int)width;
            data.imageHeight = (int)height;
            var rawBytes= image.GetData();
            if (format == HB_PAINT_IMAGE_FORMAT.BGRA)
            {
                var rawBytesLength = rawBytes.Length;
                var textureData = new NativeArray<ColorARGB>(rawBytesLength / 4, Allocator.Temp);
                int count = 0;
                for (int i = 0, ii = rawBytes.Length; i < ii; i += 4)
                    textureData[count++] = new ColorARGB(rawBytes[i+3], rawBytes[i+2], rawBytes[i+1], rawBytes[i]);
                data.finalTexture = textureData;
            }
            else // HB_PAINT_IMAGE_FORMAT.PNG, HB_PAINT_IMAGE_FORMAT.SVG To-Do: find BURST compatible decoder
                data.imageData = rawBytes;

            Debug.Log($"width {width} height {height}  format {format} {data.imageData.Length}");
            return true;
        }

        public delegate void PushTransformDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, float xx, float yx, float xy, float yy, float dx, float dy, IntPtr user_data);

        public delegate void PopDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, IntPtr user_data);

        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool ColorGlyphDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data);

        public delegate void PushClipGlyphDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_codepoint_t*/ uint glyph, IntPtr font, IntPtr user_data);

        public delegate void PushClipRectangleDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, float xmin, float ymin, float xmax, float ymax, IntPtr user_data);

        public delegate void ColorDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, bool is_foreground, uint color, IntPtr user_data);

        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool ImageDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, /*hb_blob_t*/ Blob image, uint width, uint height, HB_PAINT_IMAGE_FORMAT format, float slant, ref GlyphExtents extents, IntPtr user_data);

        public delegate void LinearOrRadialGradientDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine color_line, float x0, float y0, float x1, float y1, float x2, float y2, IntPtr user_data);

        public delegate void SweepGradientDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, ColorLine color_line, float x0, float y0, float start_angle, float end_angle, IntPtr user_data);

        public delegate void PopGroupDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, HB_PAINT_COMPOSITE_MODE mode, IntPtr user_data);

        [return: MarshalAs(UnmanagedType.I1)]
        public delegate bool CustomPalette_colorDelegate(IntPtr harfBuzzPaintFunct, ref PaintData data, uint color_index, uint color, IntPtr user_data);

    }
    public enum HB_PAINT_IMAGE_FORMAT
    {
        PNG = ('p' << 24) | ('n' << 16) | ('g' << 8) | ' ', //better would be HB.HB_TAG('c', 'p', 'c', 't'), but this does not work in C Sharp,
        SVG = ('s' << 24) | ('v' << 16) | ('g' << 8) | ' ',
        BGRA = ('B' << 24) | ('G' << 16) | ('R' << 8) | 'A',

    }
}
