using System;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;
using System.Runtime.InteropServices;
using Unity.Collections;


namespace HarfBuzz
{
    public struct Language
    {
        public IntPtr ptr;

        public Language(string language, int len)
        {
            //ptr = HB.hb_language_from_string(language, len);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(language);
            unsafe
            {
                fixed (byte* text = bytes)
                {
                    ptr = HB.hb_language_from_string(text, len);
                }
            }
        }
        //public string GetLanguage()
        //{
        //    string name;
        //    unsafe
        //    {
        //        name = Marshal.PtrToStringAnsi((IntPtr)HB.hb_language_to_string(ptr));
        //    }
        //    return name;
        //}

        //public FixedString64Bytes GetName()
        //{
        //    var result = new FixedString64Bytes();
        //    unsafe
        //    {
        //        result.AppendRawByte;
        //        var bla = HB.hb_language_to_string(ptr);
        //    }
        //}
        public Language(uint tag)
        {
            ptr = HB.hb_ot_tag_to_language(tag);
        }
        public override string ToString()
        {
            string result;
            unsafe
            {
                result = Marshal.PtrToStringAnsi((IntPtr)HB.hb_language_to_string(ptr));
            }
            return result;
        }
    }
}
