using Codice.Client.BaseCommands;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace TextMeshDOTS.TextProcessing
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    //[UpdateAfter(typeof(RegisterFontMaterialSystem))]    
    //[DisableAutoCreation]
    public partial struct ExtractTextSegmentsSystem : ISystem
    {
        EntityQuery m_query, triggerQ;
        EntityQuery fontEntityQ;
        bool m_skipChangeFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            triggerQ = SystemAPI.QueryBuilder()
                      .WithAll<FontState>()
                      //.WithAll<RebuildTextRenderTag>()
                      .WithNone<FontsDirtyTag>()
                      .Build();
            m_query = SystemAPI.QueryBuilder()
                      .WithAll<CalliByteRaw>()
                      .WithAllRW<CalliByte>()
                      .WithAllRW<TextSpan>()
                      .WithAll<TextBaseConfiguration>()
                      .WithAll<FontBlobReference>()
                      .Build();
            fontEntityQ = SystemAPI.QueryBuilder()
                  .WithAll<NativeFontPointer>() //cleanup component-->present even if font entity was destroyed
                  .WithAll<DynamicFontAsset>()  //cleanup component-->present even if font entity was destroyed
                  .WithAll<UsedGlyphs>()        // present only when font entity is valid
                  .Build();

            m_query.SetChangedVersionFilter(ComponentType.ReadWrite<CalliByteRaw>());
            m_query.AddChangedVersionFilter(ComponentType.ReadWrite<TextBaseConfiguration>());
            //m_query.SetChangedVersionFilter(ComponentType.ReadWrite<FontBlobReference>());
            fontEntityQ.SetChangedVersionFilter(ComponentType.ReadWrite<NativeFontPointer>());
            state.RequireForUpdate(triggerQ);
            m_skipChangeFilter = (state.WorldUnmanaged.Flags & WorldFlags.Editor) == WorldFlags.Editor;
        }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (m_query.IsEmpty)
                return;
            //Debug.Log("Extract text segments system");

            state.Dependency = new ExtractTextSegmentsChunkJob
            {                
                calliByteHandle = SystemAPI.GetBufferTypeHandle<CalliByte>(false),
                textSpanHandle = SystemAPI.GetBufferTypeHandle<TextSpan>(false),

                entitesHandle = SystemAPI.GetEntityTypeHandle(),
                additionalFontMaterialEntityHandle =  SystemAPI.GetBufferTypeHandle<AdditionalFontMaterialEntity>(true),
                fontBlobReferenceHandle = SystemAPI.GetComponentTypeHandle<FontBlobReference>(true),
                fontBlobReferenceLookup =  SystemAPI.GetComponentLookup<FontBlobReference>(true),
                calliByteRawHandle = SystemAPI.GetBufferTypeHandle<CalliByteRaw>(true),                
                textBaseConfigurationHandle = SystemAPI.GetComponentTypeHandle<TextBaseConfiguration>(true),

                lastSystemVersion = m_skipChangeFilter ? 0 : state.LastSystemVersion,
            }.ScheduleParallel(m_query, state.Dependency);
        }
    }
}