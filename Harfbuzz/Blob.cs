using System;


namespace HarfBuzz
{
    public struct Blob : IDisposable
    {
        public IntPtr ptr;
        public uint FaceCount => HB.hb_face_count(ptr);

        public Blob(string filename)
        {
            //ptr = HB.hb_blob_create_from_file(filename);//retunred blob is immutable
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(filename);
            unsafe
            {
                fixed (byte* text = bytes)
                {
                    ptr = HB.hb_blob_create_from_file(text);
                }
            }
        }
        public void Dispose()
        {
            HB.hb_blob_destroy(ptr);
        }
    }
}
