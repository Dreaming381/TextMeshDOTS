using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace TextMeshDOTS
{
    public static class TextRendererUtility
    {
        public static BlobAssetReference<LanguageBlob> BakeLanguage(FixedString128Bytes language)
        {
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref LanguageBlob languageBlobRoot = ref blobBuilder.ConstructRoot<LanguageBlob>();
            blobBuilder.AllocateString(ref languageBlobRoot.langugage, ref language);
            var result = blobBuilder.CreateBlobAssetReference<LanguageBlob>(Allocator.Persistent);
            blobBuilder.Dispose();
            languageBlobRoot = result.Value;
            return result;
        }

        public static TextBaseConfiguration GetTextBaseConfiguration(
            BlobAssetReference<LanguageBlob> language,
            FixedString128Bytes fontName,
            int fontSize,
            Color32 color,
            float maxLineWidth,
            HorizontalAlignmentOptions lineJustification,
            VerticalAlignmentOptions verticalAlignment,
            FontStyles fontStyles,
            FontWeight fontWeight,
            FontWidth fontWidth,
            float wordSpacing,
            float lineSpacing,
            float paragraphSpacing,
            bool isOrthographic
            )
        {
            return new TextBaseConfiguration
            {
                defaultFontFamilyHash = TextHelper.GetHashCodeCaseInsensitive(fontName),
                fontSize = (half)fontSize,
                color = color,
                maxLineWidth = maxLineWidth,
                lineJustification = HorizontalAlignmentOptions.Center,
                verticalAlignment = VerticalAlignmentOptions.MiddleTopAscentToBottomDescent,
                isOrthographic = isOrthographic,
                fontStyles = fontStyles,
                fontWeight = fontWeight,
                fontWidth = fontWidth,
                wordSpacing = (half)wordSpacing,
                lineSpacing = (half)lineSpacing,
                paragraphSpacing = (half)paragraphSpacing,
                language = language
            };
        }
        public static EntityArchetype GetTextRendererArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(14, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[1] = ComponentType.ReadWrite<GlyphOTF>();
            componentTypeStaging[2] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[3] = ComponentType.ReadWrite<XMLTag>();
            componentTypeStaging[4] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[5] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[6] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[7] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[8] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[9] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[10] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[11] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[12] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[13] = ComponentType.ReadWrite<RenderFilterSettings>();            

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
        public static EntityArchetype GetDepthSortedTextRendererArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(15, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[1] = ComponentType.ReadWrite<GlyphOTF>();
            componentTypeStaging[2] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[3] = ComponentType.ReadWrite<XMLTag>();
            componentTypeStaging[4] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[5] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[6] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[7] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[8] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[9] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[10] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[11] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[12] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[13] = ComponentType.ReadWrite<RenderFilterSettings>();
            componentTypeStaging[14] = ComponentType.ReadWrite<DepthSorted_Tag>();

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
    }
}

