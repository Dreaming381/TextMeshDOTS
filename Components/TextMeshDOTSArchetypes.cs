using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;

namespace TextMeshDOTS
{
    public static class TextMeshDOTSArchetypes
    {
        internal static EntityArchetype GetFontStateArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(2, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<FontState>();
            componentTypeStaging[1] = ComponentType.ReadWrite<FontsDirtyTag>(); //initialize Font state to `dirty` to prevent premature system updates
            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
        internal static EntityArchetype GetNativeFontDataArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(2, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<FontAssetRef>();      // do not copy FontBlobReference for this information as blob pointer will not survive scene reload
            componentTypeStaging[1] = ComponentType.ReadWrite<FontAssetMetadata>(); // do not copy FontBlobReference for this information as blob pointer will not survive scene reload
            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }

        public static EntityArchetype GetSingleFontTextArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(15, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<FontBlobReference>();            
            componentTypeStaging[1] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[2] = ComponentType.ReadWrite<GlyphOTF>();
            componentTypeStaging[3] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[4] = ComponentType.ReadWrite<XMLTag>();
            componentTypeStaging[5] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[6] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[7] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[8] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[9] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[10] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[11] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[12] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[13] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[14] = ComponentType.ReadWrite<RenderFilterSettings>();            

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
        public static EntityArchetype GetMultiFontParentTextArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(17, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<FontBlobReference>();
            componentTypeStaging[1] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[2] = ComponentType.ReadWrite<GlyphOTF>();
            componentTypeStaging[3] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[4] = ComponentType.ReadWrite<XMLTag>();
            componentTypeStaging[5] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[6] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[7] = ComponentType.ReadWrite<FontMaterialSelectorForGlyph>();
            componentTypeStaging[8] = ComponentType.ReadWrite<AdditionalFontMaterialEntity>();
            componentTypeStaging[9] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[10] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[11] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[12] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[13] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[14] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[15] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[16] = ComponentType.ReadWrite<RenderFilterSettings>();            

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
        public static EntityArchetype GetMultiFontChildTextArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(10, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<FontBlobReference>();            
            componentTypeStaging[1] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[2] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[3] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[4] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[5] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[6] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[7] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[8] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[9] = ComponentType.ReadWrite<RenderFilterSettings>();

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
    }
}

