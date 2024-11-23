using HarfBuzz;
using TextMeshDOTS;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;


namespace TextmeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
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
                              .WithAll<GlyphInfo>()
                              .WithAll<FontAssetReference>()
                              .Build();
            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<GlyphInfo>());
            glyphs = new NativeHashSet<uint>(4096, Allocator.Persistent);
        }

        protected override void OnUpdate()
        {
            if (m_query.IsEmptyIgnoreFilter)
                return;

            //Debug.Log($"Update Atlas");
            var entities = m_query.ToEntityArray(WorldUpdateAllocator);
            var glyphInfoBuffers = SystemAPI.GetBufferLookup<GlyphInfo>(true);
            for (int i = 0, length = entities.Length; i < length; i++) 
            {
                var entity = entities[i];
                var glyphInfoBuffer = glyphInfoBuffers[entity];
                var font = EntityManager.GetComponentObject<FontAssetReference>(entity).value;
                for (int j = 0, jj = glyphInfoBuffer.Length; j <jj; j++)
                {
                    var glyph = glyphInfoBuffer[j];
                    if (!glyphs.Contains(glyph.codepoint))
                    {
                        glyphs.Add(glyph.codepoint);
                        font.TryAddGlyphInternal(glyph.codepoint, out _);
                    }
                }
                var existingMaterialMeshInfo = SystemAPI.GetComponent<MaterialMeshInfo>(entity);
                if(existingMaterialMeshInfo.Material >= 0) //is runtime Material
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
