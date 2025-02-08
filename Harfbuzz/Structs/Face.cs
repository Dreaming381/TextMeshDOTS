using System;
using Unity.Collections;

namespace TextMeshDOTS.HarfBuzz
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
        public uint GetUnitsPerEM
        {
            get { return HB.hb_face_get_upem(ptr); }
            set { HB.hb_face_set_upem(ptr, value); }
        }
        public void GetSizeParams(out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end)
        {
            HB.hb_ot_layout_get_size_params(ptr, out design_size, out subfamily_id, out subfamily_name_id, out range_start, out range_end);
        }
        public void GetFaceInfo(NameID name_id, Language language, ref uint textSize, ref FixedString128Bytes text)
        {
            unsafe 
            {
                HB.hb_ot_name_get_utf8(ptr, name_id, language, ref textSize, text.GetUnsafePtr());
            }
        }

        bool HasReferenceTable(uint HB_TAG)
        {
            var blob = HB.hb_face_reference_table(ptr, HB_TAG);
            var tableLength = HB.hb_blob_get_length(blob);
            HB.hb_blob_destroy(blob);
            return tableLength > 0;
        }
        public bool HasColorBitmap()
        {
            return HasReferenceTable(HB.HB_TAG('C', 'B', 'D', 'T')) || 
                   HasReferenceTable(HB.HB_TAG('s', 'b', 'i', 'x'));
        }
        public bool HasSVG()
        {
            return HasReferenceTable(HB.HB_TAG('S', 'V', 'G', ' '));
        }
        public bool HasCOLR()
        {
            return HasReferenceTable(HB.HB_TAG('C', 'O', 'L', 'R'));
        }
        public bool HasTrueTypeOutlines()
        {
            return HasReferenceTable(HB.HB_TAG('g', 'l', 'y', 'f'));
        }
        public bool HasPostScriptOutlines()
        {
            return HasReferenceTable(HB.HB_TAG('C', 'F', 'F', ' ')) || 
                   HasReferenceTable(HB.HB_TAG('C', 'F', 'F', '2')); 
        }
        public bool IsImmutable() => HB.hb_face_is_immutable(ptr);
        public void MakeImmutable()
        {
            HB.hb_face_make_immutable(ptr);
        }
        public void Dispose()
        {
            HB.hb_face_destroy(ptr);
        }
    }
}
