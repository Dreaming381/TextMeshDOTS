using System;


namespace HarfBuzz
{
    public struct Language : IDisposable
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
        public Language(IntPtr language)
        {
            ptr = language;
        }
        public void Dispose()
        {
            HB.hb_blob_destroy(ptr);
        }
    }
}
