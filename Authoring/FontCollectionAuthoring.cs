using TextMeshDOTS;
using TextMeshDOTS.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TextmeshDOTS
{

    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/Font Collection")]
    public class FontCollectionAuthoring : MonoBehaviour
    {
        public FontCollectionAsset fontCollectionAsset;
    }
    class FontCollectionBaker : Baker<FontCollectionAuthoring>
    {
        public override void Bake(FontCollectionAuthoring authoring)
        {
            int fontCount = 0;
            if (authoring.fontCollectionAsset == null || (fontCount = authoring.fontCollectionAsset.fontRequests.Count) == 0)
                return;

            var fontRequests = new NativeArray<FontRequest>(fontCount, Allocator.Temp);

            var sourceFontRequests = authoring.fontCollectionAsset.fontRequests;
            for (int i = 0, ii = sourceFontRequests.Count; i < ii; i++)
                fontRequests[i] = sourceFontRequests[i];            

            var entity = GetEntity(TransformUsageFlags.None);
            var fontRequestsBuffer = AddBuffer<FontRequest>(entity);
            fontRequestsBuffer.AddRange(fontRequests);
        }        
    }
}
