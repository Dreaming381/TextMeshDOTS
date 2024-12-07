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
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.Authoring
{
    [BurstCompile]
    [DisableAutoCreation]
    public partial class RuntimeMultiFontTextRendererSpawner : SystemBase
    {
        bool initialized;
        int frameCount = 0;
        EntityQuery fontEntityQ;
        EntityArchetype textRenderParentArchetype, textRenderChildArchetype;
        protected override void OnCreate()
        {
            initialized = false;
            textRenderParentArchetype = TextMeshDOTSArchetypes.GetMultiFontParentTextArchetype(ref CheckedStateRef);
            textRenderChildArchetype = TextMeshDOTSArchetypes.GetMultiFontChildTextArchetype(ref CheckedStateRef);
            fontEntityQ = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<FontBlobReference, FontMaterialRef, BackEndMesh>()
                    .Build(EntityManager);
            RequireForUpdate(fontEntityQ);
        }

        protected override void OnDestroy()
        {

        }

        protected override void OnUpdate()
        {
            if (initialized)
                return;
            if (fontEntityQ.IsEmptyIgnoreFilter)
                return;

            var fontBlobReferenceEntity = fontEntityQ.GetSingletonEntity();
            var fontMaterialsBuffer = SystemAPI.GetBuffer<FontMaterialRef>(fontBlobReferenceEntity);
            var fontBlobReferences = SystemAPI.GetBuffer<FontBlobReference>(fontBlobReferenceEntity).ToNativeArray(Allocator.Temp);
            var backEndMesh = SystemAPI.GetComponent<BackEndMesh>(fontBlobReferenceEntity);

            //if (!(frameCount == 0 ^ frameCount == 100))
            if (frameCount != 0)
            {
                frameCount++;
                return;
            }

            var entitiesGraphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();

            var brgMeshID = entitiesGraphicsSystem.RegisterMesh(backEndMesh.value);
            var materialMeshInfos = CollectionHelper.CreateNativeArray<MaterialMeshInfo>(fontMaterialsBuffer.Length, WorldUpdateAllocator);
            for (int i = 0, length = fontMaterialsBuffer.Length; i < length; i++)
            {
                var brgMaterialID = entitiesGraphicsSystem.RegisterMaterial(fontMaterialsBuffer[i].value);
                materialMeshInfos[i] = new MaterialMeshInfo { MaterialID = brgMaterialID, MeshID = brgMeshID };
            }


            var textRenderControl = new TextRenderControl { flags = TextRenderControl.Flags.Dirty };
            var textBaseConfiguration = new TextBaseConfiguration
            {
                fontSize = 12, //6
                color = (Color32)Color.green,
                fontStyle = FontStyles.Normal,
                fontWeight = TextFontWeight.Regular,
                maxLineWidth = 4,
                lineJustification = HorizontalAlignmentOptions.Left,
                verticalAlignment = VerticalAlignmentOptions.TopBase,
            };

            var layer = 1;
            var filterSettings = new RenderFilterSettings
            {
                Layer = layer,
                RenderingLayerMask = (uint)(1 << layer),
                ShadowCastingMode = ShadowCastingMode.Off,
                ReceiveShadows = false,
                MotionMode = MotionVectorGenerationMode.ForceNoMotion,
                StaticShadowCaster = false,
            };

            var text0 = "Te<b>st<i> 1</b>2</i>3";
            //var text1 = "the <b>quick brown <i> fox</b> jumps</i> over the lazy dog";
            //var text2 = "<font=Noto Sans Display>Noto Sans Display</font> <font=Garamond Premier Pro>Garamond Premier Pro</font>";

            if (frameCount == 0)
            {
                int count = 100;
                float half = count * 0.5f;
                var factor = 5.0f;

                var entities = EntityManager.CreateEntity(textRenderParentArchetype, count * count, WorldUpdateAllocator);
                var additionalEntitiesArray = CollectionHelper.CreateNativeArray<Entity>(fontMaterialsBuffer.Length - 1, WorldUpdateAllocator);
                for (int x = 0; x < count; x++)
                {
                    for (int y = 0; y < count; y++)
                    {
                        var entity = entities[x * count + y];

                        var calliByteBuffer = EntityManager.GetBuffer<CalliByteRaw>(entity);
                        var calliString = new CalliString(calliByteBuffer);
                        //string text = i.ToString() + j.ToString();
                        calliString.Append(text0);

                        var localTransform = LocalTransform.FromPosition(new float3((x - half) * factor, (y - half) * factor, 0));
                        EntityManager.SetComponentData(entity, textBaseConfiguration);
                        EntityManager.SetComponentData(entity, textRenderControl);
                        var fontBlobReferencesBuffer = EntityManager.GetBuffer<FontBlobReference>(entity);
                        fontBlobReferencesBuffer.CopyFrom(fontBlobReferences);
                        EntityManager.SetComponentData(entity, localTransform);
                        EntityManager.SetComponentData(entity, materialMeshInfos[0]);
                        EntityManager.SetSharedComponent(entity, filterSettings);

                        for (int m = 1, length = materialMeshInfos.Length; m < length; m++)
                        {
                            var child = EntityManager.CreateEntity(textRenderChildArchetype);
                            additionalEntitiesArray[m - 1] = child;
                            EntityManager.SetComponentData(child, textRenderControl);
                            EntityManager.SetComponentData(child, localTransform);
                            EntityManager.SetComponentData(child, materialMeshInfos[m]);
                            EntityManager.SetSharedComponent(child, filterSettings);
                        }
                        var additionalEntities = EntityManager.GetBuffer<AdditionalFontMaterialEntity>(entity).Reinterpret<Entity>();
                        additionalEntities.AddRange(additionalEntitiesArray);
                    }
                }
                Debug.Log("MultiFont Text spawned");
            }
            frameCount++;

            //if (frameCount > 200)
            //    initialized = true;

        }
    }
}
