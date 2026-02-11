using Unity.Entities;
using UnityEngine;

namespace TextMeshDOTS
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/Debug Input")]
    public class InputAuthoring : MonoBehaviour
    {
        public InputActionAssetReference core = new();
        class InputBaker : Baker<InputAuthoring>
        {
            public override void Bake(InputAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, authoring.core);
                AddComponent<InputStates>(entity);
            }
        }
    }
}
