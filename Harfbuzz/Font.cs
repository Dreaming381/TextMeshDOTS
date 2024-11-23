using System;


namespace HarfBuzz
{
    public struct Font : IDisposable
    {
        public IntPtr ptr;

        public Font(IntPtr font)
        {
            ptr = HB.hb_font_create(font);
        }
        public float GetStyleTag(StyleTag styleTag)
        {
            return HB.hb_style_get_value(ptr, styleTag);
        }
        //public void GetPPEM(out uint x_ppem, out uint y_ppem)
        //{
        //    HB.hb_font_get_ppem(ptr, out x_ppem, out y_ppem);
        //}
        //public void SetPPEM(uint x_ppem, uint y_ppem)
        //{
        //    HB.hb_font_set_ppem(ptr, x_ppem, y_ppem);
        //}
        //public float GetPtEM()
        //{
        //    return HB.hb_font_get_ptem(ptr);
        //}
        //public void SetPtEM(float ptem)
        //{
        //    HB.hb_font_set_ptem(ptr, ptem);
        //}
        public void GetScale(out int x_scale, out int y_scale)
        {
            HB.hb_font_get_scale(ptr, out x_scale, out y_scale);
        }
        public void SetScale(int x_scale, int y_scale)
        {
            HB.hb_font_set_scale(ptr, x_scale, y_scale);
        }
        public void GetMetrics(MetricTag metricTag, out int position)
        {
            HB.hb_ot_metrics_get_position(ptr, metricTag, out position);
        }
        public bool GetGlyphExtends(uint glyph, out GlyphExtents extends)
        {
            return HB.hb_font_get_glyph_extents(ptr, glyph, out extends);
        }

        public void Dispose()
        {
            HB.hb_font_destroy(ptr);
        }
    }
}
