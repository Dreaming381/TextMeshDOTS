using System;


namespace HarfBuzz
{
    public struct Face : IDisposable
    {
        public IntPtr ptr;
        public uint GlyphCount => HB.hb_face_get_glyph_count(ptr);
        public bool HasVarData => HB.hb_ot_var_has_data(ptr);

        public Face(IntPtr blob, uint index)
        {
            ptr = HB.hb_face_create(blob, index);
        }
        public void Dispose()
        {
            HB.hb_face_destroy(ptr);
        }
    }
}
