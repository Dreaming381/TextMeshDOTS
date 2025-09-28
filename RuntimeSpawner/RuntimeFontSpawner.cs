using TextMeshDOTS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

// Use this system for adding at runtime new fonts to the fonttable

namespace TextmeshDOTS
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [DisableAutoCreation]
    partial struct RuntimeFontSpawner : ISystem
    {
        EntityQuery fontRequestQ;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            fontRequestQ = SystemAPI.QueryBuilder()
                .WithAll<FontRequest>()
                .Build();
            state.RequireForUpdate(fontRequestQ);        
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fontRequestBuffer = fontRequestQ.GetSingletonBuffer<FontRequest>();            
            var newFontRequest = GetFontRequest();
            if (!fontRequestBuffer.AsNativeArray().Contains(newFontRequest))
                fontRequestBuffer.Add(newFontRequest);
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        
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
                weight = FontWeight.Normal,
                width = 100,
                isItalic = false,
                slant = 0,
                useSystemFont = false,
                samplingPointSizeSDF = 64,
                samplingPointSizeBitmap = 64
            };
        }
    }
}
