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
        EntityQuery changedRuntimeFontMaterialQ;
        protected override void OnCreate()
        {
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            changedRuntimeFontMaterialQ = SystemAPI.QueryBuilder()
                .WithAll<RuntimeFontMaterial>()
                .Build();
            changedRuntimeFontMaterialQ.SetChangedVersionFilter(ComponentType.ReadWrite<RuntimeFontMaterial>());

            RequireForUpdate(changedRuntimeFontMaterialQ);
        }

        protected override void OnUpdate()
        {
            if (changedRuntimeFontMaterialQ.IsEmpty)
                return;            
            
            var runtimeFontMaterial = SystemAPI.GetSingletonRW<RuntimeFontMaterial>();
            var batchMaterialID = hybridRenderer.RegisterMaterial(runtimeFontMaterial.ValueRO.material);
            var batchMeshID = hybridRenderer.RegisterMesh(runtimeFontMaterial.ValueRO.backendMesh);
            runtimeFontMaterial.ValueRW.materialMeshInfo = new MaterialMeshInfo(batchMaterialID, batchMeshID);
        }
    }
}
