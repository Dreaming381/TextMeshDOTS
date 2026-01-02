using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;


namespace TextMeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    partial class RuntimeMaterialRegistration : SystemBase
    {
        // To-Do: review how to enable registration of multiple different materials
        // (e.g. unlit, 0 outline, 3 outlines, texture etc) and enable user to select them per runtime spawend TextRenderer
        EntitiesGraphicsSystem hybridRenderer;
        protected override void OnCreate()
        {
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            RequireForUpdate<RuntimeFontMaterial>();
        }

        protected override void OnUpdate()
        {
            var runtimeFontMaterial = SystemAPI.GetSingletonRW<RuntimeFontMaterial>();
            runtimeFontMaterial.ValueRW.batchMaterialID = hybridRenderer.RegisterMaterial(runtimeFontMaterial.ValueRO.material);
            runtimeFontMaterial.ValueRW.batchMeshID = hybridRenderer.RegisterMesh(runtimeFontMaterial.ValueRO.backendMesh);
            this.Enabled = false;            
        }
    }
}