using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextMeshDOTS
{
    
    [BurstCompile]
    [DisableAutoCreation]
    partial struct DebugTextRendererSpawner : ISystem
    {
        bool initialized;
        int frameCount;
        EntityArchetype textRenderArchetype;
        EntityQuery debugTextQ;
        TextBaseConfiguration textBaseConfiguration;
        RenderFilterSettings renderFilterSettings;

        public void OnCreate(ref SystemState state)
        {
            initialized = false;
            textRenderArchetype = TextRendererUtility.GetTextRendererArchetype(ref state);
            debugTextQ = SystemAPI.QueryBuilder().WithAll<DebugTextTag>().Build();

            state.RequireForUpdate<RuntimeFontMaterial>();
            state.RequireForUpdate<FontTable>();
        }
        public void OnDestroy(ref SystemState state)
        {
            if (initialized)
            {
                textBaseConfiguration.language.Dispose();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var runtimeFontMaterial = SystemAPI.GetSingleton<RuntimeFontMaterial>();
            if (!initialized)
            {
                if (runtimeFontMaterial.materialMeshInfo.MaterialID == BatchMaterialID.Null)
                    return; // material and mesh is not yet registered with EntityGraphics
                Initialize();
            }

            if (frameCount == 50)
            {
                textBaseConfiguration.color = Color.goldenRod;
                SpawnText("AA", new float3(-10, 7, 0), ref textBaseConfiguration, ref renderFilterSettings, textRenderArchetype, ref runtimeFontMaterial, ref state);
            }
            if (frameCount == 100)
            {
                textBaseConfiguration.color = Color.blue;
                SpawnText("bbb", new float3(-10, 10, 0), ref textBaseConfiguration, ref renderFilterSettings, textRenderArchetype, ref runtimeFontMaterial, ref state);
            }
            frameCount++;

            if (!SystemAPI.TryGetSingleton<InputStates>(out InputStates inputStates))
                return;

            if (inputStates.respawn != ButtonState.Canceled)
                return;

            Debug.Log($"triggered re-spawning of debug text");
            state.EntityManager.DestroyEntity(debugTextQ);
            frameCount = 0;            
        }

        void Initialize()
        {
            var language = TextRendererUtility.BakeLanguage("en");
            textBaseConfiguration = TextRendererUtility.GetTextBaseConfiguration(
                language,
                "Noto Sans Display",
                12,
                Color.white,
                30,
                HorizontalAlignmentOptions.Left,
                VerticalAlignmentOptions.TopBase,
                FontStyles.Normal,
                FontWeight.Normal,
                FontWidth.Normal,
                0, 0, 0, false);
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
            initialized = true;
        }
        void SpawnText(FixedString512Bytes text,
            float3 position,
            ref TextBaseConfiguration textBaseConfiguration,
            ref RenderFilterSettings renderFilterSettings,
            EntityArchetype textRenderArchetype,
            ref RuntimeFontMaterial runtimeFontMaterial,
            ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity(textRenderArchetype);
            state.EntityManager.AddComponent(entity, ComponentType.ReadWrite<DebugTextTag>());
            state.EntityManager.SetSharedComponent(entity, renderFilterSettings);
            var calliByteBuffer = state.EntityManager.GetBuffer<CalliByte>(entity);
            var calliString = new CalliString(calliByteBuffer);
            //string text = i.ToString() + j.ToString();
            calliString.Append(text);

            state.EntityManager.SetComponentData(entity, textBaseConfiguration);
            state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            state.EntityManager.SetComponentData(entity, runtimeFontMaterial.materialMeshInfo);

            //Debug.Log($"spawned {text}");
        }
        public struct DebugTextTag : IComponentData
        {
        }
    }
}
