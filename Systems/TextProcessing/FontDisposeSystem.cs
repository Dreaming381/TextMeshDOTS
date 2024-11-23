//using UnityEngine;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Entities;

//namespace TextMeshDOTS
//{
//    partial struct FontDisposeSystem : ISystem
//    {
//        EntityQuery m_query;
//        NativeArray<FontBlobReference> fonts;
//        [BurstCompile]
//        public void OnCreate(ref SystemState state)
//        {
//            m_query = SystemAPI.QueryBuilder()
//                          .WithAll<FontBlobReference>()
//                          .Build();
//            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontBlobReference>());
//        }

//        [BurstCompile]
//        public void OnUpdate(ref SystemState state)
//        {
//            Debug.Log("Caching Fonts");
//            m_query.ToComponentDataArray<FontBlobReference>(Allocator.Persistent);
//        }

//        [BurstCompile]
//        public void OnDestroy(ref SystemState state)
//        {
//            for (int i = 0, length = fonts.Length; i < length; i++)
//            {
//                Debug.Log($"Disposing Font {i}");
//                var font = fonts[i];
//                font.nativeFont.Dispose();
//                font.nativeFace.Dispose();
//                font.nativeBlob.Dispose();
//            }
//        }
//    }
//}
