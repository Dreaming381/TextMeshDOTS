using HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;

namespace TextMeshDOTS
{
    static class TextMeshDOTSArchetypes
    {
        //These singleton components will be added to TextRenderingUpdateSystem in OnCreate()
        public static ComponentTypeSet GetTextStatisticsTypeset()
        {
            var result = new FixedList128Bytes<ComponentType>
            {
                ComponentType.ReadWrite<GlyphCountThisFrame>(),
                ComponentType.ReadWrite<MaskCountThisFrame>(),
                ComponentType.ReadWrite<TextStatisticsTag>(),
            };
            return new ComponentTypeSet(result);
        }
        public static EntityArchetype GetNativeFontDataArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(5, Allocator.Temp);            
            componentTypeStaging[0] = ComponentType.ReadWrite<HBFontAssetRef>();
            componentTypeStaging[1] = ComponentType.ReadWrite<HBMissingGlyphs>();
            componentTypeStaging[2] = ComponentType.ReadWrite<HBUsedGlyphs>();  
            componentTypeStaging[3] = ComponentType.ReadWrite<HBUsedGlyphRects>();
            componentTypeStaging[4] = ComponentType.ReadWrite<HBFreeGlyphRects>();
            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }


        //public static EntityArchetype GetSingleFontTextArchetype(ref SystemState state)
        //{
        //    var componentTypeStaging = new NativeArray<ComponentType>(17, Allocator.Temp);
        //    componentTypeStaging[0] = ComponentType.ReadWrite<LocalTransform>();
        //    componentTypeStaging[1] = ComponentType.ReadWrite<LocalToWorld>();
        //    componentTypeStaging[2] = ComponentType.ReadWrite<FontBlobReference>();
        //    componentTypeStaging[3] = ComponentType.ReadWrite<TextBaseConfiguration>();
        //    componentTypeStaging[4] = ComponentType.ReadWrite<TextRenderControl>();
        //    componentTypeStaging[5] = ComponentType.ReadWrite<GlyphOTF>();
        //    componentTypeStaging[6] = ComponentType.ReadWrite<CalliByte>();
        //    componentTypeStaging[7] = ComponentType.ReadWrite<CalliByteRaw>();
        //    componentTypeStaging[8] = ComponentType.ReadWrite<TextSpan>();
        //    componentTypeStaging[9] = ComponentType.ReadWrite<RenderGlyph>();
        //    componentTypeStaging[10] = ComponentType.ReadWrite<TextShaderIndex>();
        //    componentTypeStaging[11] = ComponentType.ReadWrite<WorldToLocal_Tag>();
        //    componentTypeStaging[12] = ComponentType.ReadWrite<WorldRenderBounds>();
        //    componentTypeStaging[13] = ComponentType.ReadWrite<RenderBounds>();
        //    componentTypeStaging[14] = ComponentType.ReadWrite<PerInstanceCullingTag>();
        //    componentTypeStaging[15] = ComponentType.ReadWrite<MaterialMeshInfo>();
        //    componentTypeStaging[16] = ComponentType.ReadWrite<RenderFilterSettings>();            

        //    return state.EntityManager.CreateArchetype(componentTypeStaging);
        //}


        //keep in sync with RenderMeshUtility.GenerateComponentTypes
        public static ComponentTypeSet GetRenderTypeset()
        {
            var result = new FixedList128Bytes<ComponentType>
            {
                ComponentType.ReadWrite<MaterialMeshInfo>(),
            };
            return new ComponentTypeSet(result);
        }


        public static EntityArchetype GetSingleFontTextArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(17, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[1] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[2] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[3] = ComponentType.ReadWrite<TextRenderControl>();
            componentTypeStaging[4] = ComponentType.ReadWrite<GlyphOTF>();
            componentTypeStaging[5] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[6] = ComponentType.ReadWrite<CalliByteRaw>();
            componentTypeStaging[7] = ComponentType.ReadWrite<TextSpan>();
            componentTypeStaging[8] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[9] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[10] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[11] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[12] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[13] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[14] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[15] = ComponentType.ReadWrite<RenderFilterSettings>();

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
        public static EntityArchetype GetMultiFontParentTextArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(21, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[1] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[2] = ComponentType.ReadWrite<FontBlobReference>();
            componentTypeStaging[3] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[4] = ComponentType.ReadWrite<TextRenderControl>();
            componentTypeStaging[5] = ComponentType.ReadWrite<GlyphOTF>();
            componentTypeStaging[6] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[7] = ComponentType.ReadWrite<CalliByteRaw>();
            componentTypeStaging[8] = ComponentType.ReadWrite<TextSpan>();
            componentTypeStaging[9] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[10] = ComponentType.ReadWrite<RenderGlyphMask>();
            componentTypeStaging[11] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[12] = ComponentType.ReadWrite<TextMaterialMaskShaderIndex>();
            componentTypeStaging[13] = ComponentType.ReadWrite<FontMaterialSelectorForGlyph>();
            componentTypeStaging[14] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[15] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[16] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[17] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[18] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[19] = ComponentType.ReadWrite<RenderFilterSettings>();
            componentTypeStaging[20] = ComponentType.ReadWrite<AdditionalFontMaterialEntity>();

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
        public static EntityArchetype GetMultiFontChildTextArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(13, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[1] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[2] = ComponentType.ReadWrite<FontBlobReference>();
            componentTypeStaging[3] = ComponentType.ReadWrite<TextRenderControl>();
            componentTypeStaging[4] = ComponentType.ReadWrite<RenderGlyphMask>();
            componentTypeStaging[5] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[6] = ComponentType.ReadWrite<TextMaterialMaskShaderIndex>();
            componentTypeStaging[7] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[8] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[9] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[10] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[11] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[12] = ComponentType.ReadWrite<RenderFilterSettings>();

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
    }
}

