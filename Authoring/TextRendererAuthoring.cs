using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Unity.Mathematics;
using Unity.Entities.Graphics;
using Unity.Rendering;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEditor;

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
        public int samplingPointSizeSDF = 64;
        public int samplingPointSizeBitmap = 128;
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

        [Tooltip("When selected, fonts will be searched within device OS embedded fonts at runtime. De-select to bake font raw data in blob asset")]
        public bool useSystemFonts = false;
        [Tooltip("Drop here all fonts you like to use. Do not forget to include all font family members selected via Fontstyles or RichText tags such as <b> (bold), <i> (italic> or combinations thereof!")]
        public Object[] fonts;
    }

    class TestAuthoringBaker : Baker<TextRendererAuthoring>
    {
        public override void Bake(TextRendererAuthoring authoring)
        {
            var fontCount = authoring.fonts.Length;
            if (authoring.fonts == null || fontCount == 0)
                return;

            HashSet<int> redundancyCheck = new HashSet<int>(fontCount);
            for (int i = 0; i < fontCount; i++)
            {
                var font = authoring.fonts[i];
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
            AddBuffer<TextSpan>(entity);
            AddBuffer<GlyphOTF>(entity);
            AddBuffer<CalliByte>(entity);
            var calliByteRaw = AddBuffer<CalliByteRaw>(entity);
            var calliString = new CalliString(calliByteRaw);
            calliString.Append(authoring.text);
            AddComponent(entity, new TextBaseConfiguration
            {
                fontSize = authoring.fontSize,
                color = authoring.color,
                maxLineWidth = math.select(float.MaxValue, authoring.maxLineWidth, authoring.wordWrap),
                lineJustification = authoring.horizontalAlignment,
                verticalAlignment = authoring.verticalAlignment,
                isOrthographic = authoring.isOrthographic,
                fontStyles = authoring.fontStyles,
                wordSpacing = authoring.wordSpacing,
                lineSpacing = authoring.lineSpacing,
                paragraphSpacing = authoring.paragraphSpacing,
            });
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
    

    [System.Serializable]
    [Tooltip("Copy strings from system fonts here ")]
    public struct FontItem    
    {        
        public string typographicFamily;
        public string typographicSubfamily;
        public override int GetHashCode()
        {
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + TextHelper.GetHashCodeCaseInSensitive(typographicFamily);
            hashCode = hashCode * -1521134295 + TextHelper.GetHashCodeCaseInSensitive(typographicSubfamily);
            return hashCode;
        }
    }
}