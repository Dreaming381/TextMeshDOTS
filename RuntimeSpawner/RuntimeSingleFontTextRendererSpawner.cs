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
    partial struct RuntimeSingleFontTextRendererSpawner : ISystem
    {
        bool initialized;
        int frameCount;
        BlobAssetReference<FontBlob> singleFontReference;
        EntityArchetype textRenderArchetype;
        TextBaseConfiguration textBaseConfiguration;
        RenderFilterSettings renderFilterSettings;
        TextRenderControl textRenderControl;
        public void OnCreate(ref SystemState state)
        {
            initialized = false;
            textRenderArchetype = TextMeshDOTSArchetypes.GetSingleFontTextArchetype(ref state);

            var fontRequest = GetFontRequest();
            singleFontReference = FontBlobber.GetRuntimeFontBlob(fontRequest);
            textBaseConfiguration = new TextBaseConfiguration
            {
                fontSize = 12,
                color=Color.white,
                fontStyles = FontStyles.Normal,
                maxLineWidth = 30,
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
            if(singleFontReference.IsCreated) singleFontReference.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (initialized)
                return;

            //if (!(frameCount == 0 ^ frameCount == 100))
            //if (frameCount != 0)
            //{
            //    frameCount++;
            //    return;
            //}


            //if (frameCount == 0)
            //{
            //    var text1 = "äáà aâa aâ̈a bb̂b bb̂̈b bb̧b bb͜b bb︠︡b Tota persona té dret a l'educació. L'educació serà gratuïta, si més no, en la instrucció elemental i fonamental. La instrucció elemental serà obligatòria.";
            //    //var text1 = "The quick brown fox jumps over the lazy dog\n ¶";
            //    //var text2 = "Test 123";
            //    //var text3 = "ZYX";
            //    //var kerningTest = "WAVES in my Yard YAWN AT MY LAWN Toyota AWAY PALM";

            //    var entity = state.EntityManager.CreateEntity(textRenderArchetype);
            //    state.EntityManager.SetSharedComponent(entity, renderFilterSettings);
            //    var calliByteBuffer = state.EntityManager.GetBuffer<CalliByteRaw>(entity);
            //    var calliString = new CalliString(calliByteBuffer);
            //    //string text = i.ToString() + j.ToString();
            //    calliString.Append(text1);

            //    state.EntityManager.SetComponentData(entity, textBaseConfiguration);
            //    state.EntityManager.SetComponentData(entity, new FontBlobReference { value = singleFontReference });
            //    state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(-10, 7, 0)));
            //    state.EntityManager.SetComponentData(entity, textRenderControl);
            //    state.EntityManager.SetComponentEnabled(entity, ComponentType.ReadWrite<MaterialMeshInfo>(), false);
            //}

            if (frameCount == 0)
            {
                var text2 = "Test 123";
                int count = 100;
                int half = count / 2;
                var factor = 3.0f;
                textBaseConfiguration.color = Color.blue;
                //TextBackendBakingUtility.SetSubMesh(text2.Length, ref materialMeshInfo);
                var entities = state.EntityManager.CreateEntity(textRenderArchetype, count * count, state.WorldUpdateAllocator);
                for (int x = 0; x < count; x++)
                {
                    for (int y = 0; y < count; y++)
                    {
                        var entity = entities[x * count + y];
                        state.EntityManager.SetSharedComponent(entity, renderFilterSettings);
                        var calliByteBuffer = state.EntityManager.GetBuffer<CalliByte>(entity);
                        var calliString = new CalliString(calliByteBuffer);
                        //string text = i.ToString() + j.ToString();
                        calliString.Append(text2);

                        state.EntityManager.SetComponentData(entity, textBaseConfiguration);
                        state.EntityManager.SetComponentData(entity, new FontBlobReference { value = singleFontReference });
                        state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3((x - half) * factor, (y - half) * factor, 0)));
                        state.EntityManager.SetComponentData(entity, textRenderControl);
                        state.EntityManager.SetComponentEnabled(entity, ComponentType.ReadWrite<MaterialMeshInfo>(), false);
                    }
                }
                Debug.Log("Text 1 spawned");
            }

            if (frameCount == 100)
            {
                var text3 = "ZYX";
                int count = 50;
                int half = count / 2;
                var factor = 2.0f;
                textBaseConfiguration.color = Color.red;
                //TextBackendBakingUtility.SetSubMesh(text3.Length, ref materialMeshInfo);
                var entities = state.EntityManager.CreateEntity(textRenderArchetype, count * count, state.WorldUpdateAllocator);
                for (int x = 0; x < count; x++)
                {
                    for (int y = 0; y < count; y++)
                    {
                        var entity = entities[x * count + y];
                        state.EntityManager.SetSharedComponent(entity, renderFilterSettings);
                        var calliByteBuffer = state.EntityManager.GetBuffer<CalliByte>(entity);
                        var calliString = new CalliString(calliByteBuffer);
                        //string text = i.ToString() + j.ToString();
                        calliString.Append(text3);

                        state.EntityManager.SetComponentData(entity, textBaseConfiguration);
                        state.EntityManager.SetComponentData(entity, new FontBlobReference { value = singleFontReference });
                        state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3((x - half) * factor - 1, (y - half) * factor - 1, 0)));
                        state.EntityManager.SetComponentData(entity, textRenderControl);
                        state.EntityManager.SetComponentEnabled(entity, ComponentType.ReadWrite<MaterialMeshInfo>(), false);
                    }
                }
                Debug.Log("Text 2 spawned");
            }

            //if (frameCount > 200)
            //{
            //    Debug.Log($"Triggered font destruction");
            //    EntityManager.DestroyEntity(fontEntityQ);
            //    //initialized = true;
            //}

            frameCount++;
        }
        public FontRequest GetFontRequest()
        {
            //use FontUtility Scriptable Object to extract the following needed information
            //see ReadMe for more details how
            return new FontRequest
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
        }
    }
}
