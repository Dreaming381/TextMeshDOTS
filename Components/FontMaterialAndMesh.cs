using Unity.Entities;
using UnityEngine;

namespace TextMeshDOTS
{
    [InternalBufferCapacity(2)]
    public struct FontMaterialRef : IBufferElementData
    {
        public UnityObjectRef<Material> value;
    }

    public struct BackEndMesh : IComponentData
    {
        public UnityObjectRef<Mesh> value;
    }
}
