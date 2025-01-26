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
#if UNITY_EDITOR
        [Tooltip("Drop here all fonts you like to use. Do not forget to include all font family members selected via Fontstyles or RichText tags such as <b> (bold), <i> (italic> or combinations thereof!")]
        public Object[] fonts;
#endif
        public List<string> streamingAssets;

#if UNITY_EDITOR
        [MenuItem("TextMeshDOTS/Extract font path's")]
        static void ExtractFileNames()
        {
            var activeObject = Selection.activeObject;
            if (activeObject == null || !(activeObject.GetType() == typeof(GameObject) && ((GameObject)activeObject).TryGetComponent(out TextRendererAuthoring textRendererAuthoring)))
            {
                Debug.LogError($"{activeObject.GetType()} is not Gameobject");
                return;
            }
            var gameObject = activeObject as GameObject;
            

            var fonts = textRendererAuthoring.fonts;
            var streamingAssets = textRendererAuthoring.streamingAssets;
            if(streamingAssets != null )
                streamingAssets.Clear();
            else
                streamingAssets = new List<string>();
            for (int i = 0, ii = fonts.Length; i < ii; i++)
            {
                var filePath = AssetDatabase.GetAssetPath(fonts[i]);
                bool isTrueType = filePath.EndsWith("ttf", System.StringComparison.OrdinalIgnoreCase);
                bool isOpentype = filePath.EndsWith("otf", System.StringComparison.OrdinalIgnoreCase);
                if (isOpentype || isTrueType)
                    streamingAssets.Add(filePath);
                else
                {
                    Debug.LogWarning("Ensure you only have files ending with 'ttf' or 'otf' (case insensitiv) in font list");
                    streamingAssets.Clear();
                    return;
                }
            }
        }
#endif
    }

    class TestAuthoringBaker : Baker<TextRendererAuthoring>
    {
        public override void Bake(TextRendererAuthoring authoring)
        {
            if (authoring.streamingAssets == null || authoring.streamingAssets.Count == 0)
                return;

            HashSet<int> redundancyCheck = new HashSet<int>(authoring.streamingAssets.Count);
            for (int i = 0; i < authoring.streamingAssets.Count; i++)
            {
                var font = authoring.streamingAssets[i];
                var hashCode = font.GetHashCode();
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

            for (int i = 0, ii = authoring.streamingAssets.Count; i < ii; i++)
            {
                var fontItem = authoring.streamingAssets[i];
                if (i > 0)
                    AddAdditionalFontEntity(fontItem, authoring.useSystemFonts, additionalEntities, renderFilterSettings);
                else
                {
                    var fontBlobRef = BakeFontAsset(fontItem,authoring.useSystemFonts);
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
        BlobAssetReference<FontBlob> BakeFontAsset(string fontItem, bool useSystemFont)
        {            
            var customHash = new Unity.Entities.Hash128((uint)fontItem.GetHashCode(), (uint)useSystemFont.GetHashCode(), 0, 0);
            if (!TryGetBlobAssetReference(customHash, out BlobAssetReference<FontBlob> blobReference))
            {
                blobReference = FontBlobber.BakeFontBlob(fontItem, useSystemFont);

                // Register the Blob Asset to the Baker for de-duplication and reverting.
                AddBlobAssetWithCustomHash<FontBlob>(ref blobReference, customHash);
            }
            return blobReference;
        }
        void AddAdditionalFontEntity(string fontItem, bool useSystemFont, NativeList<Entity> additionalEntities, RenderFilterSettings renderFilterSettings)
        {
            var newEntity = CreateAdditionalEntity(TransformUsageFlags.Renderable);
            AddEntityGraphicsComponents(newEntity, renderFilterSettings);
            AddComponent<TextMaterialMaskShaderIndex>(newEntity);
            AddBuffer<RenderGlyphMask>(newEntity);
            AddComponent(newEntity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
            AddComponent<TextShaderIndex>(newEntity);

            var fontBlobRef = BakeFontAsset(fontItem, useSystemFont);
            AddComponent(newEntity, new FontBlobReference { value = fontBlobRef});
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