using System;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using static HarfBuzz.DelegateTest;


namespace HarfBuzz
{
    public struct Blob : IDisposable
    {
        public IntPtr ptr;
        public uint FaceCount => HB.hb_face_count(ptr);
        public uint Length => HB.hb_blob_get_length(ptr);

        //public Blob(string filename)
        //{
        //    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(filename + "\0"); //IMPORTANT! interop with c++ requieres null terminated char*
        //    unsafe
        //    {
        //        Debug.Log($"Last bytes is NULL? {bytes[^1] == 0} {bytes[^1]}");
        //        fixed (byte* text = bytes)
        //        {
        //            ptr = HB.hb_blob_create_from_file(text);
        //            Debug.Log(System.Text.Encoding.UTF8.GetString(text, bytes.Length));
        //        }
        //    }
        //}

        public Blob(string filename)
        {
            ptr = HB.hb_blob_create_from_file(filename); //returned blob is immutable            
        }
        unsafe public Blob(void* data, uint length, MemoryMode memoryMode)
        {
            ReleaseDelegate releaseDelegate = null;
            //ReleaseDelegate releaseDelegate = new ReleaseDelegate(DelegateProxies.Test);
            ptr = HB.hb_blob_create(data, length, memoryMode, (void*)IntPtr.Zero, releaseDelegate); //returned blob is immutable            
        }
        public bool IsImmutable() => HB.hb_blob_is_immutable(ptr);

        public void Dispose()
        {
            HB.hb_blob_destroy(ptr);
        }
    }
}
