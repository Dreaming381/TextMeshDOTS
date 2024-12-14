using System.Collections.Generic;
using TextMeshDOTS.Rendering.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;


namespace TextMeshDOTS.Authoring
{
    public struct OtherComponent : IComponentData
    {
        public UnityObjectRef<FontAsset> Value;
    }

    public class TestFontAuthoring : MonoBehaviour
    {
        public FontAsset fontAsset;
    }
    class TestFontAuthoringBaker : Baker<TestFontAuthoring>
    {
        public override void Bake(TestFontAuthoring authoring)
        {            
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new OtherComponent() { Value = authoring.fontAsset });
        }        
    }
}

