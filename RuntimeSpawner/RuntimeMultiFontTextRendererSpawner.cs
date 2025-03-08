using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextMeshDOTS.Authoring
{
    [BurstCompile]
    [DisableAutoCreation]
    partial struct RuntimeMultiFontTextRendererSpawner : ISystem
    {
        bool initialized;
        int frameCount;
        EntityArchetype textRenderArchetype, childTextRendererArchtype;
        TextBaseConfiguration textBaseConfiguration;
        RenderFilterSettings renderFilterSettings;
        TextRenderControl textRenderControl;
        NativeArray<FontRequest> fontRequests;
        NativeArray<BlobAssetReference<FontBlob>> fontBlobReferences;
        public void OnCreate(ref SystemState state)
        {
            initialized = false;
            textRenderArchetype = TextMeshDOTSArchetypes.GetMultiFontParentTextArchetype(ref state);
            childTextRendererArchtype = TextMeshDOTSArchetypes.GetMultiFontChildTextArchetype(ref state);

            fontRequests =new NativeArray<FontRequest>(5, Allocator.Persistent);
            InitializeFontRequests(fontRequests);
            fontBlobReferences = new NativeArray<BlobAssetReference<FontBlob>>(5, Allocator.Persistent);            
            for (int i = 0, ii = fontRequests.Length; i < ii; i++)
                fontBlobReferences[i] = FontBlobber.GetRuntimeFontBlob(fontRequests[i]);

            textBaseConfiguration = new TextBaseConfiguration
            {
                fontSize = 12,
                color = Color.white,
                fontStyles = FontStyles.Normal,
                maxLineWidth = 10,
                wordSpacing = 0,
                lineSpacing = 0,
                paragraphSpacing = 0,
                lineJustification = HorizontalAlignmentOptions.Left,
                verticalAlignment = VerticalAlignmentOptions.TopBase,
                isOrthographic = false,
            };
            var layer = 1;
            renderFilterSettings = new RenderFilterSettings
            {
                Layer = layer,
                RenderingLayerMask = (uint)(1 << layer),
                ShadowCastingMode = ShadowCastingMode.Off,
                ReceiveShadows = false,
                MotionMode = MotionVectorGenerationMode.ForceNoMotion,
                StaticShadowCaster = false,
            };
            textRenderControl = new TextRenderControl { flags = TextRenderControl.Flags.Dirty };
        }
        public void OnDestroy(ref SystemState state)
        {
            for (int i = 0, ii = fontBlobReferences.Length; i < ii; i++)
                if (fontBlobReferences[i].IsCreated) fontBlobReferences[i].Dispose();
            fontBlobReferences.Dispose();
            fontRequests.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (initialized)
                return;

            if (frameCount == 0)
            {
                var text = "Regular then <b>switch to bold, <i>then bold italic</b> then italic</i>: we are <font=Noto Color emoji>🥳</font> if it all works!";
                int count = 50;
                int half = count / 2;
                var factor = 10.0f;
                textBaseConfiguration.color = Color.green;
                //TextBackendBakingUtility.SetSubMesh(text2.Length, ref materialMeshInfo);
                var entities = state.EntityManager.CreateEntity(textRenderArchetype, count * count, state.WorldUpdateAllocator);
                var additionalEntities=new NativeList<Entity>(4, state.WorldUpdateAllocator);
                for (int x = 0; x < count; x++)
                {
                    for (int y = 0; y < count; y++)
                    {
                        var entity = entities[x * count + y];
                        state.EntityManager.SetSharedComponent(entity, renderFilterSettings);
                        var calliByteBuffer = state.EntityManager.GetBuffer<CalliByte>(entity);
                        var calliString = new CalliString(calliByteBuffer);
                        //string text = i.ToString() + j.ToString();
                        calliString.Append(text);

                        var localTransform = LocalTransform.FromPosition(new float3((x - half) * factor, (y - half) * factor, 0));
                        state.EntityManager.SetComponentData(entity, textBaseConfiguration);
                        state.EntityManager.SetComponentData(entity, new FontBlobReference { value = fontBlobReferences[0] });
                        state.EntityManager.SetComponentData(entity, localTransform);
                        state.EntityManager.SetComponentData(entity, textRenderControl);
                        state.EntityManager.SetComponentEnabled(entity, ComponentType.ReadWrite<MaterialMeshInfo>(), false);


                        for (int i = 1, ii = fontBlobReferences.Length; i < ii; i++) //add additional fonts
                        {
                            var child = state.EntityManager.CreateEntity(childTextRendererArchtype);
                            additionalEntities.Add(child);
                            state.EntityManager.SetComponentData(child, textRenderControl);
                            state.EntityManager.SetComponentData(child, new FontBlobReference { value = fontBlobReferences[i] });
                            state.EntityManager.SetComponentData(child, localTransform);
                            state.EntityManager.SetComponentEnabled(child, ComponentType.ReadWrite<MaterialMeshInfo>(), false);
                            state.EntityManager.SetSharedComponent(child, renderFilterSettings);
                        }
                        var additionalEntitiesBuffer = state.EntityManager.GetBuffer<AdditionalFontMaterialEntity>(entity).Reinterpret<Entity>();
                        additionalEntitiesBuffer.AddRange(additionalEntities.AsArray());
                        additionalEntities.Clear();
                    }
                }
                Debug.Log("Text 1 spawned");
            }            

            //if (frameCount > 200)
            //{
            //    Debug.Log($"Triggered font destruction");
            //    EntityManager.DestroyEntity(fontEntityQ);
            //    //initialized = true;
            //}

            frameCount++;
        }
        static void InitializeFontRequests(NativeArray<FontRequest> fontRequests)
        {
            //use FontUtility Scriptable Object to extract the following needed information
            //see ReadMe for more details how
            fontRequests[0] = new FontRequest
            {
                fontAssetPath = "Notosans/NotoSansDisplay-Regular.ttf",
                fontFamily = "Noto Sans Display",
                fontSubFamily = "Regular",
                typographicFamily = "",
                typographicSubfamily = "",
                fontWeight = FontWeight.Normal,
                fontWidth = 100,
                isItalic = false,
                slant = 0,
                useSystemFont = false,
                samplingPointSizeSDF = 64,
                samplingPointSizeBitmap = 64
            };
            fontRequests[1] = new FontRequest
            {
                fontAssetPath = "Notosans/NotoSansDisplay-Italic.ttf",
                fontFamily = "Noto Sans Display",
                fontSubFamily = "Italic",
                typographicFamily = "",
                typographicSubfamily = "",
                fontWeight = FontWeight.Normal,
                fontWidth = 100,
                isItalic = true,
                slant = -12,
                useSystemFont = false,
                samplingPointSizeSDF = 64,
                samplingPointSizeBitmap = 64
            };
            fontRequests[2] = new FontRequest
            {
                fontAssetPath = "Notosans/NotoSansDisplay-Bold.ttf",
                fontFamily = "Noto Sans Display",
                fontSubFamily = "Bold",
                typographicFamily = "",
                typographicSubfamily = "",
                fontWeight = FontWeight.Bold,
                fontWidth = 100,
                isItalic = false,
                slant = 0,
                useSystemFont = false,
                samplingPointSizeSDF = 64,
                samplingPointSizeBitmap = 64
            };
            fontRequests[3] = new FontRequest
            {
                fontAssetPath = "Notosans/NotoSansDisplay-BoldItalic.ttf",
                fontFamily = "Noto Sans Display",
                fontSubFamily = "Bold Italic",
                typographicFamily = "",
                typographicSubfamily = "",
                fontWeight = FontWeight.Bold,
                fontWidth = 100,
                isItalic = true,
                slant = -12,
                useSystemFont = false,
                samplingPointSizeSDF = 64,
                samplingPointSizeBitmap = 64
            };
            fontRequests[4] = new FontRequest
            {
                fontAssetPath = "Emoji/Noto-COLRv1.ttf",
                fontFamily = "Noto Color Emoji",
                fontSubFamily = "Regular",
                typographicFamily = "",
                typographicSubfamily = "",
                fontWeight = FontWeight.Normal,
                fontWidth = 100,
                isItalic = false,
                slant = 0,
                useSystemFont = false,
                samplingPointSizeSDF = 64,
                samplingPointSizeBitmap = 64
            };
        }
    }
}
