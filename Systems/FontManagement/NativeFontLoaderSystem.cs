using System;
using System.IO;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Scenes;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;


namespace TextMeshDOTS
{

    // To-Do: re-design to be able to load collection fonts (contains multiple subfamilies),
    // and variable fonts in response the requested variation axis (width, weight etc)
    // of TextRenderer (e.g. generate FontRequests after XML tag extraction)


    //[DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SceneSystemGroup))]
    partial struct NativeFontLoaderSystem : ISystem
    {
        EntityQuery changedFontRequestQ;
        NativeArray<FixedString512Bytes> systemFontsNative;
        NativeList<FontReference> systemFontReferences;
        JobHandle getFontMetadataJob;

        public void OnCreate(ref SystemState state)
        {
            //schedule fetching of metadata for installed system fonts
            var systemFonts = UnityEngine.Font.GetPathsToOSFonts();

            systemFontsNative = new NativeArray<FixedString512Bytes>(systemFonts.Length, Allocator.Persistent);
            for (int i = 0, ii = systemFonts.Length; i < ii; i++)
                systemFontsNative[i] = new FixedString512Bytes(systemFonts[i]);

            systemFontReferences = new NativeList<FontReference>(systemFonts.Length, Allocator.Persistent);
            getFontMetadataJob = new GetFontMetadataJob()
            {
                systemFonts = systemFontsNative,
                fontReferences = systemFontReferences,
            }.Schedule();

            //setup FontTable
            var perThreadFontCaches = new NativeArray<UnsafeList<Font>>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
            for (int i = 0; i < perThreadFontCaches.Length; i++)
                perThreadFontCaches[i] = new UnsafeList<Font>(64, Allocator.Persistent);
            
            state.EntityManager.CreateSingleton(new FontTable
            {
                faces = new NativeList<Face>(Allocator.Persistent),
                perThreadFontCaches = perThreadFontCaches,
                fontAssetRefs = new NativeList<FontAssetRef>(Allocator.Persistent),
                fontAssetRefToFaceIndexMap = new NativeHashMap<FontAssetRef, int>(64, Allocator.Persistent),
                variableProfiles =  new NativeList<VariableProfile>(Allocator.Persistent)
            });

            changedFontRequestQ = SystemAPI.QueryBuilder()
                .WithAll<FontReference>()
                .Build();
            changedFontRequestQ.SetChangedVersionFilter(ComponentType.ReadWrite<FontReference>());

            state.RequireForUpdate(changedFontRequestQ);
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (changedFontRequestQ.IsEmpty)
                return;

            getFontMetadataJob.Complete();

            var changedFontRequestBuffer = changedFontRequestQ.GetSingletonBuffer<FontReference>();
            var fontTable = SystemAPI.GetSingletonRW<FontTable>().ValueRW;
            state.CompleteDependency();

            //copy to nativeArray because LoadFont would invalidate DynamicBuffer due to structural changes
            var fontRequests = CollectionHelper.CreateNativeArray<FontReference>(changedFontRequestBuffer.AsNativeArray(), state.WorldUpdateAllocator);
            for (int i = 0, ii = fontRequests.Length; i < ii; i++)
            {
                var fontRequest = fontRequests[i];
                if (!fontTable.fontAssetRefToFaceIndexMap.ContainsKey(fontRequest.fontAssetRef))
                    LoadFont(fontRequest, ref state, ref fontTable);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<FontTable>().ValueRW.TryDispose(state.Dependency).Complete();
            systemFontsNative.Dispose();
            systemFontReferences.Dispose();
        }

        void LoadFont(FontReference fontReference, ref SystemState state, ref FontTable fontTable)
        {
            Blob blob;
            string fontAssetPath;
            if (fontReference.isSystemFont)
            {
                //loading rules: https://www.high-logic.com/fontcreator/manual15/fonttype.html
                if (!TryGetSystemFontReference(fontReference, out FontReference systemFontReference))
                {
                    Debug.Log($"Could not find system font {fontReference.fontFamily} {fontReference.fontSubFamily}");
                    return;
                }
                fontAssetPath = systemFontReference.filePath.ToString();
                
            }
            else
            {
                if (fontReference.streamingAssetLocationValidated)
                    fontAssetPath = Path.Combine(Application.streamingAssetsPath, fontReference.filePath.ToString());
                else
                    fontAssetPath = fontReference.filePath.ToString();

                if (!File.Exists(fontAssetPath))
                {
                    //Debug.Log($"Could not find font in {fontPath}");
                    return;
                }
            }

            blob = new Blob(fontAssetPath);
            blob.MakeImmutable();//is this neccessary considering we dispose the blob in next instruction?

            // in case font file is a collection font, chances are that none of the faces have been loaded yet
            // while file is open, load them all to avoid opening file again
            var tempFontReferences = new NativeList<FontReference>(blob.FaceCount, Allocator.Temp);
            var language = new Language(Harfbuzz.HB_TAG('E', 'N', 'G', ' '));
            TextHelper.GetFaceInfo(blob, fontAssetPath, fontReference.isSystemFont, language, tempFontReferences);

            for (int i = 0, ii = tempFontReferences.Length; i < ii; i++)
            {
                var tempFontReference = tempFontReferences[i];
                var tempFontAssetRef = tempFontReference.fontAssetRef;
                if (!fontTable.fontAssetRefToFaceIndexMap.ContainsKey(tempFontAssetRef))
                {
                    var id = fontTable.fontAssetRefToFaceIndexMap.Count;
                    fontTable.fontAssetRefs.Add(tempFontAssetRef);
                    fontTable.fontAssetRefToFaceIndexMap.Add(tempFontAssetRef, id);
                    var face = new Face(blob, tempFontReference.faceIndex);
                    face.MakeImmutable();
                    fontTable.faces.Add(face);

                    for (int k = 0, kk= fontTable.perThreadFontCaches.Length; k < kk; k++)
                    {
                        var list = fontTable.perThreadFontCaches[k];
                        list.Add(default);
                        fontTable.perThreadFontCaches[k] = list;
                    }
                }
            }

            ////test loading of collection fonts and variable fonts
            //for (int i = 0, ii = blob.FaceCount; i < ii; i++)
            //{
            //    face = new Face(blob.ptr, i);
            //    if (face.HasVarData)
            //    {
            //        var language = new Language(Harfbuzz.HB_TAG('E', 'N', 'G', ' '));
            //        Debug.Log($"found {face.AxisCount} variation axis, {face.NamedInstanceCount} named instances");

            //        //fetch a list of named variations
            //        for (int k = 0, kk = (int)face.NamedInstanceCount; k < kk; k++)
            //        {
            //            var nameID = face.GetSubFamilyNameId(k);
            //            Debug.Log($"SubFamily: {face.GetName(nameID, language)}");
            //        }

            //        //fetch a list of all variation axis
            //        face.GetAxisInfos(0, 0, out NativeList<AxisInfo> axisInfos);
            //        for (int k = 0, kk = axisInfos.Length; k < kk; k++)
            //        {
            //            var nameID = axisInfos[k].nameID;
            //            Debug.Log($"Variation axis: {face.GetName(nameID, language)}");
            //        }

            //        var foundAxisInfo = face.FindAxisInfo(AxisTag.WEIGHT, out var axisInfo);
            //        Debug.Log($"{foundAxisInfo} {axisInfo}");
            //    }
            //    face.Dispose();
            //}



            //blob can be disposed here, face and font are disposed at world shutdown via FontTable.TryDispose 
            blob.Dispose();
        }

        bool TryGetSystemFontReference(FontReference query, out FontReference fontReference)
        {
            int index;            
            if ((index = systemFontReferences.IndexOf(query)) != -1)
            {
                fontReference = systemFontReferences[index];
                return true;
            }
            fontReference = default;
            return false;
        }
        

        struct GetFontMetadataJob : IJob
        {
            [ReadOnly] public NativeArray<FixedString512Bytes> systemFonts;
            public NativeList<FontReference> fontReferences;

            public void Execute()
            {
                var language = new Language(Harfbuzz.HB_TAG('E', 'N', 'G', ' '));
                for (int i = 0, ii = systemFonts.Length; i < ii; i++)
                    TextHelper.GetFontInfo(systemFonts[i].ToString(), true, language, fontReferences);
            }
        }        
    }
}