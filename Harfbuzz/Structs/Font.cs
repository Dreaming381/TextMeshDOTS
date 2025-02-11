using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;


namespace TextMeshDOTS.HarfBuzz
{    
    public struct Font : IDisposable
    {
        public IntPtr ptr;

        public Font(IntPtr face)
        {
            ptr = HB.hb_font_create(face);
        }
        public float GetStyleTag(StyleTag styleTag)
        {
            return HB.hb_style_get_value(ptr, styleTag);
        }
        public uint2 GetPPEM()
        {
            HB.hb_font_get_ppem(ptr, out uint x_ppem, out uint y_ppem);
            return new uint2(x_ppem, y_ppem);
        }
        public void SetPPEM(uint2 ppem)
        {
            HB.hb_font_set_ppem(ptr, ppem.x, ppem.y);
        }
        public float GetPTEM()
        {
            return HB.hb_font_get_ptem(ptr);
        }
        public void SetPTEM(float ptem)
        {
            HB.hb_font_set_ptem(ptr, ptem);
        }
        public void DrawGlyph(uint glyphID, DrawDelegates drawFunctions, ref DrawData drawData)
        {
            HB.hb_font_draw_glyph(ptr, glyphID, drawFunctions, ref drawData);
        }
        public void PaintGlyph(uint glyphID, ref PaintData paintData, PaintDelegates paintFunctions, uint palette, ColorARGB foreground)
        {
            HB.hb_font_paint_glyph(ptr, glyphID, paintFunctions, ref paintData, palette, (uint)foreground.argb);
        }

        public void GetSyntheticBold(out float x_embolden, out float y_embolden, out bool in_place)
        {
            HB.hb_font_get_synthetic_bold(ptr, out x_embolden, out y_embolden, out in_place);
        }
        public float GetSynthesticSlant()
        {
            return HB.hb_font_get_synthetic_slant(ptr);
        }
        public int2 GetScale()
        {
            HB.hb_font_get_scale(ptr, out int x_scale, out int y_scale);
            return new int2(x_scale, y_scale);
        }
        public void SetScale(int x_scale, int y_scale)
        {
            HB.hb_font_set_scale(ptr, x_scale, y_scale);
        }
        public void GetMetrics(MetricTag metricTag, out int position)
        {
            HB.hb_ot_metrics_get_position(ptr, metricTag, out position);
        }
        /// <summary> Get Glyph extends form harfbuzz, but invert the height as y axis is asumed to go up in this library </summary>
        public bool GetGlyphExtents(uint glyph, out GlyphExtents extends)
        {
            var success = HB.hb_font_get_glyph_extents(ptr, glyph, out extends);
            extends.InvertY();
            return success;
        }
        public void GetFontExtentsForDirection(Direction direction, out FontExtents fontExtents)
        {
            HB.hb_font_get_extents_for_direction(ptr, direction, out fontExtents);
        }
        public void GetBaseline(Direction direction, Script script, out int baseline)
        {
            HB.hb_ot_layout_get_baseline(ptr, LayoutBaselineTag.ROMAN, direction, script, HB.HB_TAG('A', 'P', 'P', 'H'), out baseline);
        }
        public void Shape(Buffer buffer, NativeList<Feature> features)
        {
            unsafe
            {
                HB.hb_shape(ptr, buffer.ptr, (IntPtr)features.GetUnsafePtr(), (uint)features.Length);
            }
        }
        public void Shape(Buffer buffer)
        {
            HB.hb_shape(ptr, buffer.ptr, IntPtr.Zero, 0u);
        }


        //public void GetGlyphAdvanceForDirection(uint glyph, Direction direction, out int x, out int y)
        //{
        //    fixed (int* xPtr = &x)
        //    fixed (int* yPtr = &y)
        //    {
        //        HarfBuzzApi.hb_font_get_glyph_advance_for_direction(ptr, glyph, direction, xPtr, yPtr);
        //    }
        //}
        public bool IsImmutable() => HB.hb_font_is_immutable(ptr);
        public void MakeImmutable()
        {
            HB.hb_font_make_immutable(ptr);
        }
        public void Dispose()
        {
            HB.hb_font_destroy(ptr);
        }
    }
}
