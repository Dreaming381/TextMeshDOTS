using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using TextMeshDOTS.HarfBuzz.SDF;


namespace TextMeshDOTS.HarfBuzz
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
            DrawDelegates.ReleaseDelegate releaseDelegate = null;
            //ReleaseDelegate releaseDelegate = new ReleaseDelegate(DelegateProxies.Test);
            ptr = HB.hb_blob_create(data, length, memoryMode, IntPtr.Zero, releaseDelegate); //returned blob is immutable
        }
        public NativeArray<byte> GetData()
        {
            uint length;
            NativeArray<byte> result;
            unsafe
            {
                var bytes = HB.hb_blob_get_data(ptr, out length);
                result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>((void*)bytes, (int)length, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle<byte>(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            }
            return result;
        }
        public bool IsImmutable() => HB.hb_blob_is_immutable(ptr);

        public void Dispose()
        {
            HB.hb_blob_destroy(ptr);
        }
    }
}
