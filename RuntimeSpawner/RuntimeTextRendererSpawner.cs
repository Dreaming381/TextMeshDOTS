using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

// Use this system for spawning new TextRender at runtime

namespace TextMeshDOTS
{
    [BurstCompile]
    [DisableAutoCreation]
    partial struct RuntimeTextRendererSpawner : ISystem
    {
        bool initialized;
        int frameCount;
        EntityArchetype textRenderArchetype;
        TextBaseConfiguration textBaseConfiguration;
        RenderFilterSettings renderFilterSettings;
        FixedString512Bytes text1, text2, text3, text4, kerningTest;

        public void OnCreate(ref SystemState state)
        {
            initialized = false;
            textRenderArchetype = TextRendererUtility.GetTextRendererArchetype(ref state); //renders faster than depth sorted but rendering order can be incorrect
            //textRenderArchetype = TextRendererUtility.GetDepthSortedTextRendererArchetype(ref state); //ensures correct rendering but significant performance impact
            text1 = "äáà aâa aâ̈a bb̂b bb̂̈b bb̧b bb͜b bb︠︡b Tota persona té dret a l'educació. L'educació serà gratuïta, si més no, en la instrucció elemental i fonamental. La instrucció elemental serà obligatòria.";
            text2 = "The quick brown fox jumps over the lazy dog\n ¶";
            text3 = "Test 123";
            text4 = "ZYX";
            kerningTest = "WAVES in my Yard YAWN AT MY LAWN Toyota AWAY PALM";


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

            if (frameCount == 10)
            {
                textBaseConfiguration.SetFamily("Noto Sans Display");
                textBaseConfiguration.color = Color.goldenRod;
                textBaseConfiguration.maxLineWidth = 30;
                SpawnText(text3, new float3(-10, 7, 0), ref textBaseConfiguration, ref renderFilterSettings, textRenderArchetype, ref runtimeFontMaterial, ref state);
            }

            if (frameCount == 50)
            {
                textBaseConfiguration.SetFamily("Arial");
                textBaseConfiguration.color = Color.blue;
                textBaseConfiguration.maxLineWidth = 3;
                SpawnTextArray(text3, 50, 3, ref textBaseConfiguration, ref renderFilterSettings, textRenderArchetype, ref runtimeFontMaterial, ref state);
            }

            if (frameCount == 100)
            {
                textBaseConfiguration.SetFamily("Arial");
                textBaseConfiguration.color = Color.red;
                textBaseConfiguration.maxLineWidth = 3;
                SpawnTextArray(text4, 15, 2, ref textBaseConfiguration, ref renderFilterSettings, textRenderArchetype, ref runtimeFontMaterial, ref state);
            }

            frameCount++;
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
        static void SpawnTextArray(
            FixedString512Bytes text, 
            int count, 
            float spreadFactor,
            ref TextBaseConfiguration textBaseConfiguration,
            ref RenderFilterSettings renderFilterSettings,
            EntityArchetype textRenderArchetype,
            ref RuntimeFontMaterial runtimeFontMaterial,
            ref SystemState state)
        {
            int half = count / 2;            
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
                    calliString.Append(text);

                    state.EntityManager.SetComponentData(entity, textBaseConfiguration);
                    state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3((x - half) * spreadFactor - 1, (y - half) * spreadFactor - 1, 0)));
                    state.EntityManager.SetComponentData(entity, runtimeFontMaterial.materialMeshInfo);
                }
            }
            Debug.Log($"spawned {count * count} instances of {text}");
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
           
                    state.EntityManager.SetSharedComponent(entity, renderFilterSettings);
                    var calliByteBuffer = state.EntityManager.GetBuffer<CalliByte>(entity);
                    var calliString = new CalliString(calliByteBuffer);
                    //string text = i.ToString() + j.ToString();
                    calliString.Append(text);

                    state.EntityManager.SetComponentData(entity, textBaseConfiguration);
                    state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
                    state.EntityManager.SetComponentData(entity, runtimeFontMaterial.materialMeshInfo);

            Debug.Log($"spawned {text}");
        }
    }
}
