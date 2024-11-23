using HarfBuzz;
using TextMeshDOTS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Font = HarfBuzz.Font;

namespace TextmeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    //[RequireMatchingQueriesForUpdate]
    partial class LoadNativeFont : SystemBase
    {
        EntityQuery m_query;
        NativeFontReference nativeFontReference;
        protected override void OnCreate()
        {
            m_query = SystemAPI.QueryBuilder()
                              .WithAll<FontBlobReference>()
                              .WithNone<NativeFontReference>()
                              .Build();
            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontBlobReference>());
        }

        protected override void OnUpdate()
        {
            if (m_query.IsEmpty)
                return;
            Debug.Log($"Caching Fonts");
            var entities = m_query.ToEntityArray(Allocator.Temp);
            EntityManager.AddComponent(entities, ComponentType.ReadWrite<NativeFontReference>());

            if (!nativeFontReference.isCreated)
            {
                var nativeBlob = new Blob("Assets\\Resources\\NotoSansDisplay-Regular.ttf");
                var nativFace = new Face(nativeBlob.ptr, 0);
                var nativeFont = new Font(nativFace.ptr);
                nativeFontReference = new NativeFontReference { isCreated = true, nativeBlob = nativeBlob, nativeFace = nativFace, nativeFont = nativeFont };
            }
            else
                Debug.LogError($"Font already cached!");

            for (int i = 0; i < entities.Length; i++)
            {
                EntityManager.SetComponentData(entities[i], nativeFontReference);
            }
        }

        protected override void OnDestroy()
        {
            Debug.Log($"Dispose Font");
            nativeFontReference.nativeFont.Dispose();
            nativeFontReference.nativeFace.Dispose();
            nativeFontReference.nativeBlob.Dispose();
        }
    }
}
