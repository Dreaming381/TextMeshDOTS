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
using UnityEngine.TextCore.Text;


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
            //font.ListSomeInfo();
            
            
            var fontReferences = AddBuffer<FontBlobReference>(entity);
            AddComponent(entity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
            AddComponent<TextShaderIndex>(entity);
            var fontBlobRef = BakeFontAsset(fontAsset);
            fontReferences.Add(new FontBlobReference { blob = fontBlobRef });
            AddComponentObject(entity, new FontAssetReferences { value = authoring.fontAssets });

            AddBuffer<RenderGlyph>(entity);

            if (authoring.fontAssets.Count > 1)
            {
                AddComponent<TextMaterialMaskShaderIndex>(entity);
                AddBuffer<FontMaterialSelectorForGlyph>(entity);
                AddBuffer<RenderGlyphMask>(entity);
                var additionalEntities = AddBuffer<AdditionalFontMaterialEntity>(entity).Reinterpret<Entity>();
                for (int i = 1, length= authoring.fontAssets.Count; i <length ; i++)
                {
                    var newEntity = CreateAdditionalEntity(TransformUsageFlags.Renderable);
                    fontAsset = authoring.fontAssets[i];
                    if (fontAsset == null)
                        continue;
                    fontAsset.ReadFontAssetDefinition();

                    //BakeFontAsset(newEntity, font);
                    //AddComponentObject(newEntity, new FontAssetReference { value = font });
                    AddComponent(newEntity, new TextRenderControl { flags = TextRenderControl.Flags.Dirty });
                    AddComponent<TextShaderIndex>(newEntity);
                    fontBlobRef = BakeFontAsset(fontAsset);
                    fontReferences.Add(new FontBlobReference { blob = fontBlobRef });

                    AddComponent<TextMaterialMaskShaderIndex>(newEntity);
                    AddBuffer<RenderGlyphMask>(newEntity);
                    additionalEntities.Add(newEntity);
                   
                    //add all components MeshRendererBaker would add to a single rendered entity 
                    AddEntityGraphicsComponents(newEntity, fontAsset, backEndMesh);
                    //add MeshRendererBakingData to trick RenderMeshPostProcessSystem to process this entity
                    //important for incremental baking to update MaterialMeshInfo
                    this.AddMeshRendererBakingData(newEntity, meshRenderer);
                }
            }

            //Text Content
            AddBuffer<TextSpan>(entity);
            AddBuffer<GlyphOTF>(entity);
            AddBuffer<CalliByte>(entity);
            var calliByteRaw = AddBuffer<CalliByteRaw>(entity);            
            var calliString = new CalliString(calliByteRaw);
            calliString.Append(authoring.text);
            //calliByte.RemoveAt(calliByte.Length -1);
            AddComponent(entity, new TextBaseConfiguration
            {
                fontSize          = authoring.fontSize,
                color             = authoring.color,
                maxLineWidth      = math.select(float.MaxValue, authoring.maxLineWidth, authoring.wordWrap),
                lineJustification = authoring.horizontalAlignment,
                verticalAlignment = authoring.verticalAlignment,
                isOrthographic    = authoring.isOrthographic,
                fontStyle         = authoring.fontStyle,
                wordSpacing = authoring.wordSpacing,
                lineSpacing = authoring.lineSpacing,
                paragraphSpacing = authoring.paragraphSpacing,
            });
        }

        BlobAssetReference<FontBlob> BakeFontAsset(FontAsset fontAsset)
        {
            var customHash = new Unity.Entities.Hash128((uint)fontAsset.GetHashCode(), 0, 0, 0);
            if (!TryGetBlobAssetReference(customHash, out BlobAssetReference<FontBlob> blobReference))
            {
                blobReference = FontBlobber.BakeFontBlob(fontAsset);

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
    }
}

