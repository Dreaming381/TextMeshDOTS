using System;
using Unity.Rendering;


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
        public uint UnitsPerEM
        {
            get { return HB.hb_face_get_upem(ptr); }
            set { HB.hb_face_set_upem(ptr, value); }
        }
        public void GetSizeParams(out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end)
        {
            HB.hb_ot_layout_get_size_params(ptr, out design_size, out subfamily_id, out subfamily_name_id, out range_start, out range_end);
        }
        public bool IsImmutable() => HB.hb_face_is_immutable(ptr);
        public void Dispose()
        {
            HB.hb_face_destroy(ptr);
        }
    }
}
