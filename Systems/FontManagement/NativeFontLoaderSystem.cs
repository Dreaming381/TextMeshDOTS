using System;
using System.IO;
using System.Reflection;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
//using static TextMeshDOTS.TextCoreExtensions;
using Font = TextMeshDOTS.HarfBuzz.Font;


namespace TextMeshDOTS
{
    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    partial class NativeFontLoaderSystem : SystemBase
    {
        EntityQuery changedFontRequestQ;
        MethodInfo methodInfo;
        FieldInfo[] fontReference;
        object m_fontRef;

        protected override void OnCreate()
        {
            var fontAssetRefToFaceIndexMap = new NativeHashMap<FontAssetRef, int>(64, Allocator.Persistent);
            var perThreadFontCaches = new NativeArray<UnsafeList<Font>>(Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndexCount, Allocator.Persistent);
            for (int i = 0; i < perThreadFontCaches.Length; i++)
            {
                perThreadFontCaches[i] = new UnsafeList<Font>(64, Allocator.Persistent);
            }
            EntityManager.CreateSingleton(new FontTable
            {
                faces = new NativeList<Face>(Allocator.Persistent),
                perThreadFontCaches = perThreadFontCaches,
                fontAssetRefs = new NativeList<FontAssetRef>(Allocator.Persistent),
                fontAssetRefToFaceIndexMap = fontAssetRefToFaceIndexMap,               
            });

            changedFontRequestQ = SystemAPI.QueryBuilder()
                .WithAll<FontRequest>()
                .Build();
            changedFontRequestQ.SetChangedVersionFilter(ComponentType.ReadWrite<FontRequest>());

            RequireForUpdate(changedFontRequestQ);

            GetSystemFontsMethod();
        }


        void GetSystemFontsMethod()
        {
            Assembly textCoreFontEngineModule = default;
            foreach (Assembly loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loadedAssembly.GetName().Name == "UnityEngine.TextCoreFontEngineModule")
                {
                    textCoreFontEngineModule = loadedAssembly;
                    //Debug.Log($"Found UnityEngine.TextCoreFontEngineModule in loaded assemblies: {loadedAssembly.FullName}");
                    break;
                }
            }
            var fontReferenceType = textCoreFontEngineModule.GetType("UnityEngine.TextCore.LowLevel.FontReference");
            fontReference = fontReferenceType.GetFields();
            var m_fontRef = Activator.CreateInstance(fontReferenceType);

            BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Static;
            methodInfo = typeof(FontEngine).GetMethod("TryGetSystemFontReference", bindingFlags);
            //MakeDelegate<fontReference>(methodInfo);
        }
        public static Func<string, string, object, bool> MakeDelegate<U>(MethodInfo methodInfo)
        {
            var f = (Func<string, string, U, bool>)Delegate.CreateDelegate(typeof(Func<string, string, U, bool>), methodInfo);
            return (a, b, c) => f(a, b, (U)c);
        }

        //[BurstCompile]
        protected override void OnUpdate()
        {
            if (changedFontRequestQ.IsEmpty)
                return;

            var changedFontRequestBuffer = changedFontRequestQ.GetSingletonBuffer<FontRequest>();
            var fontTable = SystemAPI.GetSingletonRW<FontTable>().ValueRW;
            CompleteDependency();

            //copy to nativeArray because LoadFont would invalidate DynamicBuffer due to structural changes
            var fontRequests = CollectionHelper.CreateNativeArray<FontRequest>(changedFontRequestBuffer.AsNativeArray(), WorldUpdateAllocator);            

            for (int i = 0, ii = fontRequests.Length; i < ii; i++)
            {
                var fontRequest = fontRequests[i];
                if (!fontTable.fontAssetRefToFaceIndexMap.ContainsKey(fontRequest.fontAssetRef))
                {
                    LoadFont(fontRequest, ref CheckedStateRef, ref fontTable);
                }
            }
        }

        protected override void OnDestroy()
        {
            SystemAPI.GetSingletonRW<FontTable>().ValueRW.TryDispose(Dependency).Complete();
        }
        void LoadFont(FontRequest fontRequest, ref SystemState state, ref FontTable fontTable)
        {
            Blob blob;
            var typeographicFamilyDataMissing = (fontRequest.typographicFamily.IsEmpty || fontRequest.typographicSubfamily.IsEmpty);
            var family = typeographicFamilyDataMissing ? fontRequest.fontFamily : fontRequest.typographicFamily;
            var subFamily = typeographicFamilyDataMissing ? fontRequest.fontSubFamily : fontRequest.typographicSubfamily;

            if (fontRequest.useSystemFont)
            {
                //loading rules: https://www.high-logic.com/fontcreator/manual15/fonttype.html

                object[] args = new object[] { family.ToString(), subFamily.ToString(), m_fontRef };
                var systemFontFound = (bool)methodInfo.Invoke(null, args);
                var result = args[2];

                //if (!TryGetSystemFontReference(family.ToString(), subFamily.ToString(), out UnityFontReference unityFontReference))
                if(!systemFontFound)
                {
                    Debug.Log($"Could not find system font {family} {subFamily}");
                    return;
                }
                else
                {
                    //Debug.Log($"Found {fieldInfos[0].GetValue(result)} {fieldInfos[1].GetValue(result)} {fieldInfos[2].GetValue(result)} {fieldInfos[3].GetValue(result)}");
                    blob = new Blob((string)fontReference[3].GetValue(result));
                    blob.MakeImmutable();//is this neccessary considering we dispose the blob in next instruction?
                }
            }
            else
            {
                string fontPath;
                if (fontRequest.streamingAssetLocationValidated)
                    fontPath = Path.Combine(Application.streamingAssetsPath, fontRequest.fontAssetPath.ToString());
                else
                    fontPath = fontRequest.fontAssetPath.ToString();

                if (!File.Exists(fontPath))
                {
                    //Debug.Log($"Could not find font in {fontPath}");
                    return;
                }
                else
                {
                    blob = new Blob(fontPath);
                    blob.MakeImmutable();//is this neccessary considering we dispose the blob in next instruction?
                }
            }             

            if (!fontTable.fontAssetRefToFaceIndexMap.ContainsKey(fontRequest.fontAssetRef))
            {
                var id = fontTable.fontAssetRefToFaceIndexMap.Count;
                fontTable.fontAssetRefs.Add(fontRequest.fontAssetRef);
                fontTable.fontAssetRefToFaceIndexMap.Add(fontRequest.fontAssetRef, id);
                var face = new Face(blob.ptr, 0);
                face.MakeImmutable();
                fontTable.faces.Add(face);
                for (int i = 0; i < fontTable.perThreadFontCaches.Length; i++)
                {
                    var list = fontTable.perThreadFontCaches[i];
                    list.Add(default);
                    fontTable.perThreadFontCaches[i] = list;
                }
            }

            //blob can be disposed here, face and font are disposed at world shutdown via FontTable.TryDispose 
            blob.Dispose();
        }        
    }
}