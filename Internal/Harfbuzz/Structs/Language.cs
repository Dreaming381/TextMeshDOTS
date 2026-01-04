using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace TextMeshDOTS.HarfBuzz
{
    public struct Language
    {
        internal IntPtr ptr;


        /// <summary> Converts str representing a BCP 47 language tag to the corresponding hb_language_t object </summary>
        public Language(FixedString32Bytes language)
        {
            unsafe
            {
                ptr = Harfbuzz.hb_language_from_string(language.GetUnsafePtr(), language.Length);
            }
        }
        public FixedString32Bytes LanguageToFixedString()
        {
            FixedString32Bytes result;
            unsafe
            {
                var bla = Harfbuzz.hb_language_to_string(ptr);
                result = Harfbuzz.GetFixedString32(bla);
            }
            return result;
        }
        /// <summary>
        /// Converts captial letter <see href="https://learn.microsoft.com/en-us/typography/opentype/spec/languagetags">Opentype language tags</see> into BCP 47 language subtags
        /// </summary>
        public Language(uint tag)
        {
            ptr = Harfbuzz.hb_ot_tag_to_language(tag);
        }
        static public Language English => new Language(Harfbuzz.HB_TAG('E', 'N', 'G', ' '));
        static public Language Undefined => new Language("und");
        public static Language Default => new Language { ptr = Harfbuzz.hb_language_get_default() };        
        
        public override string ToString()
        {
            string result;
            unsafe
            {
                result = Marshal.PtrToStringUTF8((IntPtr)Harfbuzz.hb_language_to_string(ptr));
            }
            return result;
        }
    }
}
