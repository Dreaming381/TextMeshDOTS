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
        /// <summary>
        /// Convert a BCP 47 compliant langugage string into a blob asset. Risk of creating leaks when creating 
        /// such a blobasset at runtime is high: ensure to dispose it when there are no more BlobAssetReferences to this blob asset. 
        /// This is a non issue for the baking workflow as this is handled automatically. 
        /// </summary>     
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

        /// <summary>
        /// Get TextBaseConfiguration IComponent by providing all required data. Note: for achitectural 
        /// reasons we cannot avoid providing the language as blob asset. Risk of creating leaks when creating 
        /// such a blob asset at runtime is high: ensure to dispose it when there are no more BlobAssetReferences
        /// to this blob asset. This is a non issue for the baking workflow as this is handled automatically. 
        /// </summary>
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
                lineJustification = lineJustification,
                verticalAlignment = verticalAlignment,
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
        /// <summary>
        /// Get Get TextRendererArchetype to create TextRenderer entites at runtime via APIs such as 
        /// state.EntityManager.CreateEntity(textRenderArchetype). Note: this archetype does not contain the DepthSorted_Tag, 
        /// which significanlty increases performance, but can result in unexpected overdraw.
        /// </summary>
        public static EntityArchetype GetTextRendererArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(12, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[1] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[2] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[3] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[4] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[5] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[6] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[7] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[8] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[9] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[10] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[11] = ComponentType.ReadWrite<RenderFilterSettings>();            

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
        /// <summary>
        /// Get Get TextRendererArchetype to create TextRenderer entites at runtime via APIs such as 
        /// state.EntityManager.CreateEntity(textRenderArchetype). Note: this archetype contains the DepthSorted_Tag, 
        /// which ensures correct rendering but breaks instancing and batching, incuring a significant performance penalty.
        /// </summary>
        public static EntityArchetype GetDepthSortedTextRendererArchetype(ref SystemState state)
        {
            var componentTypeStaging = new NativeArray<ComponentType>(13, Allocator.Temp);
            componentTypeStaging[0] = ComponentType.ReadWrite<TextBaseConfiguration>();
            componentTypeStaging[1] = ComponentType.ReadWrite<CalliByte>();
            componentTypeStaging[2] = ComponentType.ReadWrite<RenderGlyph>();
            componentTypeStaging[3] = ComponentType.ReadWrite<TextShaderIndex>();
            componentTypeStaging[4] = ComponentType.ReadWrite<LocalTransform>();
            componentTypeStaging[5] = ComponentType.ReadWrite<LocalToWorld>();
            componentTypeStaging[6] = ComponentType.ReadWrite<WorldToLocal_Tag>();
            componentTypeStaging[7] = ComponentType.ReadWrite<WorldRenderBounds>();
            componentTypeStaging[8] = ComponentType.ReadWrite<RenderBounds>();
            componentTypeStaging[9] = ComponentType.ReadWrite<PerInstanceCullingTag>();
            componentTypeStaging[10] = ComponentType.ReadWrite<MaterialMeshInfo>();
            componentTypeStaging[11] = ComponentType.ReadWrite<RenderFilterSettings>();
            componentTypeStaging[12] = ComponentType.ReadWrite<DepthSorted_Tag>();

            return state.EntityManager.CreateArchetype(componentTypeStaging);
        }
    }
}

