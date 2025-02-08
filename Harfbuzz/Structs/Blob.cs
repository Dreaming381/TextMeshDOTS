using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using static TextMeshDOTS.HarfBuzz.DrawDelegates;
using Unity.Burst;
using AOT;
using UnityEngine;


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
            //FunctionPointer<ReleaseDelegate> releaseFunctionPointer = BurstCompiler.CompileFunctionPointer<ReleaseDelegate>(OnBlobDisposed);
            ReleaseDelegate releaseDelegate = new ReleaseDelegate(OnBlobDisposed);
            ptr = HB.hb_blob_create(data, length, memoryMode, IntPtr.Zero, releaseDelegate); //returned blob is immutable
        }
        //public Blob(string filename, out bool success)
        //{
        //    ptr = HB.hb_blob_create_from_file_or_fail(filename); //returned blob is immutable
        //    success = ptr != IntPtr.Zero;
        //}
        //unsafe public Blob(void* data, uint length, MemoryMode memoryMode, out bool success)
        //{
        //    DrawDelegates.ReleaseDelegate releaseDelegate = null;
        //    //ReleaseDelegate releaseDelegate = new ReleaseDelegate(DelegateProxies.Test);
        //    ptr = HB.hb_blob_create_or_fail(data, length, memoryMode, IntPtr.Zero, releaseDelegate); //returned blob is immutable
        //    success = ptr != IntPtr.Zero;
        //}
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
        public void MakeImmutable()
        {
            HB.hb_blob_make_immutable(ptr);
        }

        public void Dispose()
        {
            HB.hb_blob_destroy(ptr);
        }

        [MonoPInvokeCallback(typeof(ReleaseDelegate))]
        public static void OnBlobDisposed()
        {
            //Debug.Log($"harfbuzz: blob was disposed");
        }
    }
}
