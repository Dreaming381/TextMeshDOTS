using HarfBuzz;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace TextMeshDOTS.TextProcessing
{    
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(GlyphHashmapSystem))]
    //[RequireMatchingQueriesForUpdate]
    partial class UpdateAtlasSystem : SystemBase
    {
        EntityQuery m_query;
        EntitiesGraphicsSystem hybridRenderer;

        NativeHashSet<uint> glyphs;
        protected override void OnCreate()
        {
            hybridRenderer = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            m_query = SystemAPI.QueryBuilder()
                              .WithAll<GlyphOTF>()
                              .WithAll<FontAssetReference>()
                              .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<GlyphOTF>());
            glyphs = new NativeHashSet<uint>(4096, Allocator.Persistent);
            SystemAPI.TryGetSingletonRW<FontAtlasInfo>(out RefRW<FontAtlasInfo> fontAtlasInfo);
        }

        protected override void OnUpdate()
        {
            if (m_query.IsEmptyIgnoreFilter)
                return;

            Dependency.Complete();
            if (!SystemAPI.TryGetSingletonEntity<FontAtlasInfo>(out Entity fontManagerEntity))
                return;

            var fontAtlasInfo = SystemAPI.GetComponent<FontAtlasInfo>(fontManagerEntity);
            var missingGlyphs = fontAtlasInfo.missingGlyphs;
            
            if (missingGlyphs.Length > 0)
            {
                Debug.Log($"Update Atlas");
                var glyphAtlas = fontAtlasInfo.glyphAtlas;

                var entities = m_query.ToEntityArray(WorldUpdateAllocator);
                var entity = entities[0];

                var font = EntityManager.GetComponentObject<FontAssetReference>(entity).value;
                for (int j = 0, jj = missingGlyphs.Length; j < jj; j++)
                {
                    var glyphID = missingGlyphs[j];
                    font.TryAddGlyphInternal(glyphID, out _);
                    glyphAtlas.Add(glyphID);
                }
                missingGlyphs.Clear();

                var existingMaterialMeshInfo = SystemAPI.GetComponent<MaterialMeshInfo>(entity);
                if (existingMaterialMeshInfo.Material >= 0) //is runtime Material
                {
                    hybridRenderer.GetMaterial(existingMaterialMeshInfo.MaterialID);
                    hybridRenderer.UnregisterMaterial(existingMaterialMeshInfo.MaterialID);
                }
                var brgMaterialID = hybridRenderer.RegisterMaterial(font.material);
                existingMaterialMeshInfo.MaterialID = brgMaterialID;
                SystemAPI.SetComponent<MaterialMeshInfo>(entity, existingMaterialMeshInfo);
            }
        }

        protected override void OnDestroy()
        {
            glyphs.Dispose();
        }
    }
}
