//using UnityEngine;
//using Unity.Entities;
//using Unity.Collections;



//namespace TextMeshDOTS.Authoring
//{

//    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
//    partial struct FontMaterialCleanupBakingSystem : ISystem
//    {
//        EntityQuery queryCleanupTag, textRendererQ, testQ;
//        public void OnCreate(ref SystemState state)
//        {
//            queryCleanupTag = SystemAPI.QueryBuilder()
//                .WithAll<FontMaterial>()
//                .Build();
//            textRendererQ = SystemAPI.QueryBuilder()
//                              .WithAll<FontBlobReference>()
//                              .WithNone<FontMaterial>()
//                              .WithNone<GlyphsInUse>()
//                              .Build();
//            testQ = SystemAPI.QueryBuilder()
//                              .WithAll<FontBlobReference>()
//                              .WithAll<FontMaterial>()
//                              .WithNone<GlyphsInUse>()
//                              .Build();

//        }
//        public void OnUpdate(ref SystemState state)
//        {
//            Debug.Log($"Baker: {queryCleanupTag.CalculateEntityCount()}  {textRendererQ.CalculateEntityCount()} {testQ.CalculateEntityCount()}");

//            Debug.Log($"Remove {queryCleanupTag.CalculateEntityCount()} font materials");
//            //state.EntityManager.RemoveComponent<FontMaterial>(queryCleanupTag);
//        }
//    }
//}

