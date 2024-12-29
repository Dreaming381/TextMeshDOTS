using System;
using System.Runtime.InteropServices;


namespace TextMeshDOTS.HarfBuzz
{
    public struct Language
    {
        public IntPtr ptr;

        //public Language(string language, int len)
        //{
        //    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(language);
        //    unsafe
        //    {
        //        fixed (byte* text = bytes)
        //        {
        //            ptr = HB.hb_language_from_string(text, len);
        //        }
        //    }
        //}
        //public Language(string language)
        //{
        //    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(language + "\0"); //IMPORTANT! interop with c++ requieres null terminated char*
        //    //byte[] bytes = System.Text.Encoding.UTF8.GetBytes(language); 
        //    unsafe
        //    {
        //        //Debug.Log($"Last bytes is NULL? {bytes[^1] == 0} {bytes[^1]}");
        //        fixed (byte* text = bytes)
        //        {
        //            ptr = HB.hb_language_from_string(text, -1);
        //            //Debug.Log(System.Text.Encoding.UTF8.GetString(text, bytes.Length));
        //        }
        //    }
        //}

        public Language(string language, int len)
        {
            ptr = HB.hb_language_from_string(language, len);
        }
        public Language(string language)
        {
            ptr = HB.hb_language_from_string(language, -1);
        }
        /// <summary>
        /// Converts captial letter <see href="https://learn.microsoft.com/en-us/typography/opentype/spec/languagetags">Opentype language tags</see> into BCP 47 language subtags
        /// </summary>
        public Language(uint tag)
        {
            ptr = HB.hb_ot_tag_to_language(tag);
        }
        public override string ToString()
        {
            string result;
            unsafe
            {
                result = Marshal.PtrToStringUTF8(HB.hb_language_to_string(ptr));
            }
            return result;
        }
    }
}
