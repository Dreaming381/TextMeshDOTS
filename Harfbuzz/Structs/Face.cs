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
        public uint UnitsPerEM
        {
            get { return HB.hb_face_get_upem(ptr); }
            set { HB.hb_face_set_upem(ptr, value); }
        }
        public void GetSizeParams(out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end)
        {
            HB.hb_ot_layout_get_size_params(ptr, out design_size, out subfamily_id, out subfamily_name_id, out range_start, out range_end);
        }
        public void GetFaceInfo(HB_OT_NAME_ID name_id, Language language, ref uint textSize, ref FixedString128Bytes text)
        {
            unsafe 
            {
                HB.hb_ot_name_get_utf8(ptr, name_id, language, ref textSize, text.GetUnsafePtr());
            }
        }
        public bool HasTrueTypeOutlines()
        {
            var glyf = HB.HB_TAG('g', 'l', 'y', 'f');

            var blob = HB.hb_face_reference_table(ptr, glyf);
            var glyfTableLength = HB.hb_blob_get_length(blob);
            HB.hb_blob_destroy(blob);
            return glyfTableLength > 0;
        }
        public bool HasPostScriptOutlines()
        {
            var cff = HB.HB_TAG('C', 'F', 'F', ' ');
            var cff2 = HB.HB_TAG('C', 'F', 'F', '2');

            var blob = HB.hb_face_reference_table(ptr, cff);
            var cffTableLength = HB.hb_blob_get_length(blob);
            HB.hb_blob_destroy(blob);

            blob = HB.hb_face_reference_table(ptr, cff2);
            var cff2TableLength = HB.hb_blob_get_length(blob);
            HB.hb_blob_destroy(blob);

            return cffTableLength > 0 || cff2TableLength > 0;
        }
        public bool IsImmutable() => HB.hb_face_is_immutable(ptr);
        public void Dispose()
        {
            HB.hb_face_destroy(ptr);
        }
    }
}
