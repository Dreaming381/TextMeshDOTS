using System.Collections.Generic;
using TextMeshDOTS.Rendering.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;


namespace TextMeshDOTS.Authoring
{
    public struct EntityPrefabComponent : IComponentData
    {
        public Entity Value;
    }

    public class TestAuthoring : MonoBehaviour
    {
        public GameObject fontAsset;
    }
    class TestAuthoringBaker : Baker<TestAuthoring>
    {
        public override void Bake(TestAuthoring authoring)
        {
            // Register the Prefab in the Baker
            var entityPrefab = GetEntity(authoring.fontAsset, TransformUsageFlags.None);
            // Add the Entity reference to a component for instantiation later
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new EntityPrefabComponent() { Value = entityPrefab });
        }        
    }
}

