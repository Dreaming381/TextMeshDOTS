using HarfBuzz;
using System.Collections.Generic;
using TextMeshDOTS.Rendering;
using TextMeshDOTS.Rendering.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;


namespace TextMeshDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TextMeshDOTS/Text Renderer")]
    public class TextRendererAuthoring : MonoBehaviour
    {
        [TextArea(5, 10)]
        public string text;

        public float                      fontSize            = 12f;
        public bool                       wordWrap            = true;
        public float                      maxLineWidth        = float.MaxValue;
        public HorizontalAlignmentOptions horizontalAlignment = HorizontalAlignmentOptions.Left;
        public VerticalAlignmentOptions   verticalAlignment   = VerticalAlignmentOptions.TopAscent;
        public bool                       isOrthographic      = false;
        public FontStyles                 fontStyle           = FontStyles.Normal;
        [Tooltip("Additional word spacing in font units where a value of 1 equals 1/100em.")]
        public float wordSpacing = 0;
        [Tooltip("Additional line spacing in font units where a value of 1 equals 1/100em.")]
        public float lineSpacing = 0;
        [Tooltip("Paragraph spacing in font units where a value of 1 equals 1/100em.")]
        public float paragraphSpacing = 0;

        public Color32 color = Color.white;
        public List<FontAsset> fontAssets;        
    }


    [TemporaryBakingType]
    internal class TextRendererBaker : Baker<TextRendererAuthoring>
    {
        public override void Bake(TextRendererAuthoring authoring)
        {
            if (authoring.fontAssets == null || authoring.fontAssets.Count == 0 || authoring.fontAssets[0] == null)
                return;

            var backEndMesh = Resources.Load<Mesh>(TextBackendBakingUtility.kTextBackendMeshResource);

            //add MeshFilter and MeshRender on main entity to ensure it correctly converted 
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = authoring.gameObject.AddComponent<MeshRenderer>();
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = authoring.gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = backEndMesh;
            meshRenderer.material = authoring.fontAssets[0].material;

            var entity = GetEntity(TransformUsageFlags.Renderable);

            //Fonts
            var fontAsset = authoring.fontAssets[0];
            fontAsset.ReadFontAssetDefinition();
            
            AddComponent(entity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
            AddComponent<TextShaderIndex>(entity);

            List<FontAsset> fontAssetReferences = new List<FontAsset>();
            var additionalEntities = new NativeList<Entity>(16, Allocator.Temp);
            var fontBlobReferences = new NativeList<FontBlobReference>(16, Allocator.Temp);            

            //add Regular FontWeight
            var fontBlobRef = BakeFontAsset(fontAsset, TextFontWeight.Regular, false);
            fontBlobReferences.Add(new FontBlobReference { blob = fontBlobRef });
            fontAssetReferences.Add(fontAsset);
            //add Regular FontWeight - Italic
            var fontWeightPair = fontAsset.fontWeightTable[TextCoreExtensions.GetTextFontWeightIndex(TextFontWeight.Regular)];
            if (fontWeightPair.italicTypeface != null)
                AddAdditionalFontEntity(entity, fontWeightPair.italicTypeface, TextFontWeight.Regular, true, fontAssetReferences, fontBlobReferences, additionalEntities, backEndMesh, meshRenderer);

            //add additional FontWeights. Bold is pretty much always needed and should always be set by user.
            //Consider to parse the entire fontAsset.fontWeightTable instead of only Bold
            //also consider this creates a lot of entities that are posisbly not needed / not rendered.
            //-->disable MaterialMeshInfo for all non used entities once this is clear after parsing TextSpans?
            AddFontWeightPair(TextFontWeight.Bold, entity, fontAsset, fontAssetReferences, fontBlobReferences, additionalEntities, backEndMesh, meshRenderer);

            //now do the same for all additional fonts            
            if (authoring.fontAssets.Count > 1)
            {
                for (int i = 1, length = authoring.fontAssets.Count; i < length; i++)
                {
                    fontAsset = authoring.fontAssets[i];
                    AddAdditionalFontEntity(entity, fontAsset, TextFontWeight.Regular, false, fontAssetReferences, fontBlobReferences, additionalEntities, backEndMesh, meshRenderer);
                    AddFontWeightPair(TextFontWeight.Regular, entity, fontAsset, fontAssetReferences, fontBlobReferences, additionalEntities, backEndMesh, meshRenderer); //for regular FontWeight, this call will just add italic
                    AddFontWeightPair(TextFontWeight.Bold, entity, fontAsset, fontAssetReferences, fontBlobReferences, additionalEntities, backEndMesh, meshRenderer); 
                }
            }
            var fontReferencesBuffer = AddBuffer<FontBlobReference>(entity);
            fontReferencesBuffer.AddRange(fontBlobReferences.AsArray());
            if (additionalEntities.Length > 0)
            {
                var additionalEntitiesBuffer = AddBuffer<AdditionalFontMaterialEntity>(entity);
                additionalEntitiesBuffer.Reinterpret<Entity>().AddRange(additionalEntities.AsArray());
                AddComponent<TextMaterialMaskShaderIndex>(entity);
                AddBuffer<FontMaterialSelectorForGlyph>(entity);
                AddBuffer<RenderGlyphMask>(entity);
            }
            AddComponentObject(entity, new FontAssetReferences { value = fontAssetReferences });

            //Text Content
            AddBuffer<TextSpan>(entity);
            AddBuffer<GlyphOTF>(entity);
            AddBuffer<CalliByte>(entity);
            var calliByteRaw = AddBuffer<CalliByteRaw>(entity);            
            var calliString = new CalliString(calliByteRaw);
            calliString.Append(authoring.text);
            AddComponent(entity, new TextBaseConfiguration
            {
                fontSize          = authoring.fontSize,
                color             = authoring.color,
                maxLineWidth      = math.select(float.MaxValue, authoring.maxLineWidth, authoring.wordWrap),
                lineJustification = authoring.horizontalAlignment,
                verticalAlignment = authoring.verticalAlignment,
                isOrthographic    = authoring.isOrthographic,
                fontStyle         = authoring.fontStyle,
                fontWeight = (authoring.fontStyle & FontStyles.Bold)==FontStyles.Bold ? TextFontWeight.Bold : TextFontWeight.Regular,
                wordSpacing = authoring.wordSpacing,
                lineSpacing = authoring.lineSpacing,
                paragraphSpacing = authoring.paragraphSpacing,
            });
            AddBuffer<RenderGlyph>(entity);
        }

        BlobAssetReference<FontBlob> BakeFontAsset(FontAsset fontAsset, TextFontWeight textFontWeight, bool isItalic)
        {
            var customHash = new Unity.Entities.Hash128((uint)fontAsset.GetHashCode(), 0, 0, 0);
            if (!TryGetBlobAssetReference(customHash, out BlobAssetReference<FontBlob> blobReference))
            {
                blobReference = FontBlobber.BakeFontBlob(fontAsset, textFontWeight, isItalic);

                // Register the Blob Asset to the Baker for de-duplication and reverting.
                AddBlobAssetWithCustomHash<FontBlob>(ref blobReference, customHash);
            }
            return blobReference;        
        }
        void AddEntityGraphicsComponents(Entity entity, FontAsset fontAsset, Mesh backEndMesh)
        {
            DependsOn(fontAsset);
            var layer = GetLayer();
            var renderMeshDescription = new RenderMeshDescription
            {
                FilterSettings = new RenderFilterSettings
                {
                    Layer = layer,
                    RenderingLayerMask = (uint)(1 << layer),
                    ShadowCastingMode = ShadowCastingMode.Off,
                    ReceiveShadows = false,
                    MotionMode = MotionVectorGenerationMode.Object,
                    StaticShadowCaster = false,
                },
                LightProbeUsage = LightProbeUsage.Off,
            };
            this.BakeMeshAndMaterial(entity, renderMeshDescription, backEndMesh, fontAsset.material);            
        }

        void AddFontWeightPair(TextFontWeight textFontWeight, Entity mainEntity, FontAsset mainFontAsset,
            List<FontAsset> fontAssetReferences,
            NativeList<FontBlobReference> fontBlobReferences,
            NativeList<Entity> additionalEntities,
            Mesh backEndMesh,
            MeshRenderer meshRenderer)
        {
            // add fontWeight
            var fontWeightPair = mainFontAsset.fontWeightTable[TextCoreExtensions.GetTextFontWeightIndex(textFontWeight)];
            if (fontWeightPair.regularTypeface != null)
                AddAdditionalFontEntity(mainEntity, fontWeightPair.regularTypeface, textFontWeight, false, fontAssetReferences, fontBlobReferences, additionalEntities, backEndMesh, meshRenderer);

            //add fontWeight italic
            if (fontWeightPair.italicTypeface != null)
                AddAdditionalFontEntity(mainEntity, fontWeightPair.italicTypeface, textFontWeight, true, fontAssetReferences, fontBlobReferences, additionalEntities, backEndMesh, meshRenderer);
        }
        void AddAdditionalFontEntity(Entity entity, 
            FontAsset fontAsset, 
            TextFontWeight textFontWeight, 
            bool isItalic, 
            List<FontAsset> fontAssetReferences,
            NativeList<FontBlobReference> fontBlobReferences, 
            NativeList<Entity> additionalEntities, 
            Mesh backEndMesh, 
            MeshRenderer meshRenderer)
        {           
            var newEntity = CreateAdditionalEntity(TransformUsageFlags.Renderable);
            fontAsset.ReadFontAssetDefinition();

            AddComponent(newEntity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
            AddComponent<TextShaderIndex>(newEntity);
            var fontBlobRef = BakeFontAsset(fontAsset, textFontWeight, isItalic);
            fontAssetReferences.Add(fontAsset);
            fontBlobReferences.Add(new FontBlobReference { blob = fontBlobRef });
            additionalEntities.Add(newEntity);

            AddComponent<TextMaterialMaskShaderIndex>(newEntity);
            AddBuffer<RenderGlyphMask>(newEntity);            

            //add all components MeshRendererBaker would add to a single rendered entity 
            AddEntityGraphicsComponents(newEntity, fontAsset, backEndMesh);
            //add MeshRendererBakingData to trick RenderMeshPostProcessSystem to process this entity
            //important for incremental baking to update MaterialMeshInfo
            this.AddMeshRendererBakingData(newEntity, meshRenderer);            
        }
    }
}

