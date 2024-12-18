using HarfBuzz;
using System;
using System.Collections.Generic;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Unity.Mathematics;
using Unity.Entities.Graphics;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace TextMeshDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/Text Renderer")]
    public class TextRendererAuthoring : MonoBehaviour
    {
        [TextArea(5, 10)]
        public string text;
        public FontStyles fontStyle = FontStyles.Normal;
        public float fontSize = 12f;
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

        [Header("Font References")]
        [Tooltip("First font is default regular style, additional fonts are selectable via richtext tags such a <b> <i>")]
        public FontItem[] fonts;

        [Header("System Fonts")]
        [Tooltip("Right Click to see permissable value for fonts. Paste them as-is into font list.")]
        [ContextMenuItem("Get SystemFonts", "GetSystemFonts")]
        public List<string> systemFonts;
        void GetSystemFonts()
        {
            //var systemFontReferences = Font.GetOSInstalledFontNames();
            var systemFontReferences = TextCoreExtensions.GetSystemFontRef();
            //systemFontReferences.Sort(new UnityFontReferenceComparer());

            systemFonts.Clear();
            foreach (var font in systemFontReferences)
                systemFonts.Add($"{font.familyName} - {font.styleName}");
            systemFonts.Sort();
        }
    }

    class TestAuthoringBaker : Baker<TextRendererAuthoring>
    {
        public override void Bake(TextRendererAuthoring authoring)
        {
            if (authoring.fonts == null || authoring.fonts.Length == 0)
                return;

            for (int i = 0; i < authoring.fonts.Length; i++)
                if (authoring.fonts[i].fontName == null || authoring.fonts[i].fontName==String.Empty)
                    return;

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

            for (int i = 0, ii= authoring.fonts.Length; i < ii; i++)
            {
                var fontItem = authoring.fonts[i];                
                if(i > 0)
                    AddAdditionalFontEntity(fontItem, additionalEntities, renderFilterSettings);
                else
                {
                    var fontBlobRef = BakeFontAsset(fontItem);
                    AddComponent(entity, new FontBlobReference { fontBlob = fontBlobRef });
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
                fontStyle = authoring.fontStyle,
                fontWeight = (authoring.fontStyle & FontStyles.Bold) == FontStyles.Bold ? TextFontWeight.Bold : TextFontWeight.Regular,
                wordSpacing = authoring.wordSpacing,
                lineSpacing = authoring.lineSpacing,
                paragraphSpacing = authoring.paragraphSpacing,
            });
            AddBuffer<RenderGlyph>(entity);

        }
        BlobAssetReference<FontBlob> BakeFontAsset(FontItem fontItem)
        {
            var split = fontItem.fontName.Split(" - ");
            var fontFamily = split[0];
            var fontSubFamily = split[1];

            var fontFamilyHash = TextHelper.GetHashCodeCaseInSensitive(fontFamily);
            var fontAssetRef = new FontAssetRef(fontFamilyHash, fontItem.fontWeight, fontItem.fontStyle);

            var customHash = new Unity.Entities.Hash128((uint)fontAssetRef.GetHashCode(), 0, 0, 0);
            if (!TryGetBlobAssetReference(customHash, out BlobAssetReference<FontBlob> blobReference))
            {
                blobReference = FontBlobber.BakeFontBlob(fontItem, fontAssetRef, fontFamily, fontSubFamily);

                // Register the Blob Asset to the Baker for de-duplication and reverting.
                AddBlobAssetWithCustomHash<FontBlob>(ref blobReference, customHash);
            }
            return blobReference;
        }
        void AddAdditionalFontEntity(FontItem fontItem, NativeList<Entity> additionalEntities, RenderFilterSettings renderFilterSettings)
        {
            var newEntity = CreateAdditionalEntity(TransformUsageFlags.Renderable);
            AddEntityGraphicsComponents(newEntity, renderFilterSettings);
            AddComponent<TextMaterialMaskShaderIndex>(newEntity);
            AddBuffer<RenderGlyphMask>(newEntity);
            AddComponent(newEntity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
            AddComponent<TextShaderIndex>(newEntity);

            var fontBlobRef = BakeFontAsset(fontItem);
            AddComponent(newEntity, new FontBlobReference { fontBlob = fontBlobRef });
            additionalEntities.Add(newEntity);
        }

        //keep in sync with RenderMeshUtility.GenerateComponentTypes
        void AddEntityGraphicsComponents(Entity entity, RenderFilterSettings renderFilterSettings)
        {
            AddComponent<WorldRenderBounds>(entity);
            AddSharedComponent(entity, renderFilterSettings);
            //AddComponent<MaterialMeshInfo>(entity); //add once font texture was generated and registered with BRG        
            AddComponent<WorldToLocal_Tag>(entity);
            AddComponent<RenderBounds>(entity);
            AddComponent<PerInstanceCullingTag>(entity);
        }
    }
    

    [Serializable]
    public struct FontItem
    {
        [Tooltip("Copy strings from system fonts here ")]
        public string fontName;
        public TextFontWeight fontWeight;
        [EnumButtons]
        public Style fontStyle;
    }
}