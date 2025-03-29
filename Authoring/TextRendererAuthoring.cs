using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities.Graphics;
using Unity.Rendering;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace TextMeshDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/Text Renderer")]
    public class TextRendererAuthoring : MonoBehaviour
    {
        [TextArea(5, 10)]
        public string text;
        [EnumButtons]
        public FontStyles fontStyles = FontStyles.Normal;
        public float fontSize = 12f;

        [Tooltip("Sampling point size is used to set the font scale. See https://harfbuzz.github.io/harfbuzz-hb-font.html#hb-font-set-scale")]
        [Range(64, 96)]
        public int samplingPointSizeSDF = 64;
        [Range(64, 256)]
        public int samplingPointSizeBitmap = 64;
        public Color32 color = Color.white;

        public HorizontalAlignmentOptions horizontalAlignment = HorizontalAlignmentOptions.Left;
        public VerticalAlignmentOptions verticalAlignment = VerticalAlignmentOptions.TopAscent;
        public bool wordWrap = true;
        public float maxLineWidth = 30;
        public bool isOrthographic = false;
        [Tooltip("Additional word spacing in font units where a value of 1 equals 1/100em.")]
        public float wordSpacing = 0;
        [Tooltip("Additional line spacing in font units where a value of 1 equals 1/100em.")]
        public float lineSpacing = 0;
        [Tooltip("Paragraph spacing in font units where a value of 1 equals 1/100em.")]
        public float paragraphSpacing = 0;

        [Tooltip("When selected, fonts will be searched within device OS embedded fonts at runtime. Otherwise fonts need to be located in StreamingAssets folder")]
        public bool useSystemFonts = false;
        [Tooltip("Drop here all fonts and their family members you like to use. Family members are selected based on choosen FontStyle.)")]
        public Object[] fonts;
        //public FontCollectionAsset fontCollectionAsset;
        //public string selectedFont;
    }

    class TextRendererBaker : Baker<TextRendererAuthoring>
    {
        public override void Bake(TextRendererAuthoring authoring)
        {
            int fontCount = 0;
            if (authoring.fonts == null || (fontCount = authoring.fonts.Length) == 0)
                return;

            HashSet<int> redundancyCheck = new HashSet<int>(fontCount);
            for (int i = 0; i < fontCount; i++)
            {
                var font = authoring.fonts[i];
                if(font == null) 
                    return;
                var hashCode = font.name.GetHashCode();
                if (redundancyCheck.Contains(hashCode))
                {
                    //Debug.Log($"List of fonts contains redundancies");
                    return;
                }
                redundancyCheck.Add(hashCode);
            }

            var layer = GetLayer();

            var renderFilterSettings = new RenderFilterSettings
            {
                Layer = layer,
                RenderingLayerMask = (uint)(1 << layer),
                ShadowCastingMode = ShadowCastingMode.Off,
                ReceiveShadows = false,
                MotionMode = MotionVectorGenerationMode.Object,
                StaticShadowCaster = false,
            };

            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddEntityGraphicsComponents(entity, renderFilterSettings);
            AddComponent(entity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
            AddComponent<TextShaderIndex>(entity);

            var additionalEntities = new NativeList<Entity>(16, Allocator.Temp);

            for (int i = 0; i < fontCount; i++)
            {
                var fontItem = authoring.fonts[i];
                if (i > 0)
                    AddAdditionalFontEntity(fontItem, authoring.useSystemFonts, authoring.samplingPointSizeSDF, authoring.samplingPointSizeBitmap, additionalEntities, renderFilterSettings);
                else
                {
                    var fontBlobRef = BakeFontAsset(fontItem,authoring.useSystemFonts, authoring.samplingPointSizeSDF, authoring.samplingPointSizeBitmap);
                    AddComponent(entity, new FontBlobReference { value = fontBlobRef });
                }
            }

            if (additionalEntities.Length > 0)
            {
                var additionalEntitiesBuffer = AddBuffer<AdditionalFontMaterialEntity>(entity);
                additionalEntitiesBuffer.Reinterpret<Entity>().AddRange(additionalEntities.AsArray());
                AddComponent<TextMaterialMaskShaderIndex>(entity);
                AddBuffer<FontMaterialSelectorForGlyph>(entity);
                AddBuffer<RenderGlyphMask>(entity);
            }

            //Text Content
            AddBuffer<XMLTag>(entity);
            AddBuffer<GlyphOTF>(entity);
            var calliByte = AddBuffer<CalliByte>(entity);
            var calliString = new CalliString(calliByte);
            calliString.Append(authoring.text);
            var textBaseConfiguraton = new TextBaseConfiguration
            {
                fontSize = authoring.fontSize,
                color = authoring.color,                
                maxLineWidth = math.select(float.MaxValue, authoring.maxLineWidth, authoring.wordWrap),
                lineJustification = authoring.horizontalAlignment,
                verticalAlignment = authoring.verticalAlignment,
                isOrthographic = authoring.isOrthographic,
                fontStyles = authoring.fontStyles,
                fontWeight = (authoring.fontStyles & FontStyles.Bold)== FontStyles.Bold ? FontWeight.Bold : FontWeight.Normal,
                fontWidth = (int)FontWidth.Normal, //cannot be set from UI, 
                wordSpacing = authoring.wordSpacing,
                lineSpacing = authoring.lineSpacing,
                paragraphSpacing = authoring.paragraphSpacing,
            };
            AddComponent(entity, textBaseConfiguraton);
            AddBuffer<RenderGlyph>(entity);
        }
        BlobAssetReference<FontBlob> BakeFontAsset(Object fontItem, bool useSystemFont, int samplingPointSizeSDF, int samplingPointSizeBitmap)
        {            
            var customHash = new Unity.Entities.Hash128((uint)fontItem.GetHashCode(), (uint)useSystemFont.GetHashCode(), 0, 0);
            if (!TryGetBlobAssetReference(customHash, out BlobAssetReference<FontBlob> blobReference))
            {
                blobReference = FontBlobber.BakeFontBlob(fontItem, useSystemFont, samplingPointSizeSDF, samplingPointSizeBitmap);

                // Register the Blob Asset to the Baker for de-duplication and reverting.
                AddBlobAssetWithCustomHash<FontBlob>(ref blobReference, customHash);
            }
            return blobReference;
        }
        void AddAdditionalFontEntity(Object fontItem, bool useSystemFont, int samplingPointSizeSDF, int samplingPointSizeBitmap, NativeList<Entity> additionalEntities, RenderFilterSettings renderFilterSettings)
        {
            var newEntity = CreateAdditionalEntity(TransformUsageFlags.Renderable);
            AddEntityGraphicsComponents(newEntity, renderFilterSettings);
            AddComponent<TextMaterialMaskShaderIndex>(newEntity);
            AddBuffer<RenderGlyphMask>(newEntity);
            AddComponent(newEntity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
            AddComponent<TextShaderIndex>(newEntity);

            var fontBlobRef = BakeFontAsset(fontItem, useSystemFont, samplingPointSizeSDF, samplingPointSizeBitmap);
            AddComponent(newEntity, new FontBlobReference { value = fontBlobRef});
            additionalEntities.Add(newEntity);
        }

        //keep in sync with RenderMeshUtility.GenerateComponentTypes
        void AddEntityGraphicsComponents(Entity entity, RenderFilterSettings renderFilterSettings)
        {
            AddComponent<WorldRenderBounds>(entity);
            AddSharedComponent(entity, renderFilterSettings);
            AddComponent<MaterialMeshInfo>(entity); 
            SetComponentEnabled<MaterialMeshInfo>(entity, false); //enable once font texture was generated and registered with BRG
            AddComponent<WorldToLocal_Tag>(entity);
            AddComponent<RenderBounds>(entity);
            AddComponent<PerInstanceCullingTag>(entity);
        }
    }    
}