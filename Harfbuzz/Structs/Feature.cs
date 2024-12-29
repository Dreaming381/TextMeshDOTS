using System.Runtime.InteropServices;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Feature
    {
        public uint tag;
        public uint value;
        public uint start;
        public uint end;
        //public Blob(string feature)
        //{
        //    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(feature + "\0"); //IMPORTANT! interop with c++ requieres null terminated char*
        //    unsafe
        //    {
        //        Debug.Log($"Last bytes is NULL? {bytes[^1] == 0} {bytes[^1]}");
        //        fixed (byte* text = bytes)
        //        {
        //            bool result = HB.hb_feature_from_string(text, -1, out this);
        //            Debug.Log(System.Text.Encoding.UTF8.GetString(text, bytes.Length));
        //        }
        //    }
        //}
        public Feature(string feature)
        {
            bool result = HB.hb_feature_from_string(feature, -1, out this);
        }
        public Feature(uint tag, uint value, uint start, uint end)
        {
            //features.Add(new Feature() { tag = HB.HB_TAG('s', 'm', 'c', 'p'), value = 1, start = (uint)textSpan.startIndex, end = (uint)(textSpan.startIndex + textSpan.length), });
            this.tag = tag;
            this.value = value;
            this.start = start;
            this.end = end;
        }
        
    }
}
