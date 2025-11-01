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
            var fontAssetRefToFaceIndexMap = new NativeHashMap<FontAssetRef, int>(64, Allocator.Persistent);
            var perThreadFontCaches = new NativeArray<UnsafeList<Font>>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
            for (int i = 0; i < perThreadFontCaches.Length; i++)
            {
                perThreadFontCaches[i] = new UnsafeList<Font>(64, Allocator.Persistent);
            }
            state.EntityManager.CreateSingleton(new FontTable
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

            state.RequireForUpdate(changedFontRequestQ);
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (changedFontRequestQ.IsEmpty)
                return;

            getFontMetadataJob.Complete();

            var changedFontRequestBuffer = changedFontRequestQ.GetSingletonBuffer<FontRequest>();
            var fontTable = SystemAPI.GetSingletonRW<FontTable>().ValueRW;
            state.CompleteDependency();

            //copy to nativeArray because LoadFont would invalidate DynamicBuffer due to structural changes
            var fontRequests = CollectionHelper.CreateNativeArray<FontRequest>(changedFontRequestBuffer.AsNativeArray(), state.WorldUpdateAllocator);

            for (int i = 0, ii = fontRequests.Length; i < ii; i++)
            {
                var fontRequest = fontRequests[i];
                if (!fontTable.fontAssetRefToFaceIndexMap.ContainsKey(fontRequest.fontAssetRef))
                {
                    LoadFont(fontRequest, ref state, ref fontTable);
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<FontTable>().ValueRW.TryDispose(state.Dependency).Complete();
            systemFontsNative.Dispose();
            systemFontReferences.Dispose();
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

                if (!TryGetSystemFontReference(fontRequest, out FontReference fontReference))
                {
                    Debug.Log($"Could not find system font {family} {subFamily}");
                    return;
                }
                else
                {
                    blob = new Blob(fontReference.filePath.ToString());
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

                ////test loading of collection fonts and variable fonts
                //for (int i = 0, ii = blob.FaceCount; i < ii; i++)
                //{
                //    face = new Face(blob.ptr, i);
                //    if(face.HasVarData)
                //    {
                //        var language = new Language(Harfbuzz.HB_TAG('E', 'N', 'G', ' '));
                //        Debug.Log($"found {face.AxisCount} variation axis, {face.NamedInstanceCount} named instances");

                //        //fetch a list of named variations
                //        for (int k = 0, kk = (int)face.NamedInstanceCount; k < kk; k++)
                //            Debug.Log($"nameID for index {k}: {face.GetSubFamilyNameId(k)} {face.GetName(face.GetSubFamilyNameId(k), language)} ");                        

                //        //fetch a list of all variation axis
                //        face.GetAxisInfos(0, 0, out NativeList<AxisInfo> axisInfos);
                //        for (int k = 0, kk = axisInfos.Length; k < kk; k++)
                //            Debug.Log($"{axisInfos[k]} (axis name: {face.GetName(axisInfos[k].nameID, language)})");                        

                //        //var foundAxisInfo = face.FindAxisInfo((uint)Axis.WEIGHT, out var axisInfo);
                //        //Debug.Log($"{foundAxisInfo} {axisInfo}");
                //    }
                //    face.Dispose();
                //}
            }
            //blob can be disposed here, face and font are disposed at world shutdown via FontTable.TryDispose 
            blob.Dispose();
        }

        bool TryGetSystemFontReference(FontRequest fontRequest, out FontReference fontReference)
        {
            int index;
            var query = new FontReference { fontFamily = fontRequest.fontFamily, fontSubFamily = fontRequest.fontSubFamily, typographicFamily = fontRequest.typographicFamily, typographicSubfamily = fontRequest.typographicSubfamily };
            if ((index = systemFontReferences.IndexOf(query)) != -1)
            {
                fontReference = systemFontReferences[index];
                return true;
            }
            fontReference = default;
            return false;
        }
        static bool GetFontInfo(string fontAssetPath, Language language, NativeList<FontReference> fontReferences)
        {
            bool isTrueType = fontAssetPath.EndsWith("ttf", System.StringComparison.OrdinalIgnoreCase);
            bool isTrueTypeCollection = fontAssetPath.EndsWith("ttc", System.StringComparison.OrdinalIgnoreCase);
            bool isOpentype = fontAssetPath.EndsWith("otf", System.StringComparison.OrdinalIgnoreCase);
            if (isOpentype || isTrueType || isTrueTypeCollection)
            {
                var blob = new Blob(fontAssetPath);
                //if (isTrueTypeCollection)
                //    Debug.Log(fontAssetPath);
                for (int i = 0, ii = blob.FaceCount; i < ii; i++)
                {
                    var face = new Face(blob.ptr, i);
                    var fontReference = new FontReference();
                    fontReference.filePath = fontAssetPath;
                    fontReference.faceIndex = i;
                    fontReference.fontFamily = face.GetName(NameID.FONT_FAMILY, language);
                    fontReference.fontSubFamily = face.GetName(NameID.FONT_SUBFAMILY, language);
                    fontReference.typographicFamily = face.GetName(NameID.TYPOGRAPHIC_FAMILY, language);
                    fontReference.typographicSubfamily = face.GetName(NameID.TYPOGRAPHIC_SUBFAMILY, language);
                    fontReferences.Add(fontReference);

                    if (isTrueTypeCollection)
                    {
                        var font = new Font(face.ptr);
                        var weight = (int)font.GetStyleTag(StyleTag.WEIGHT);
                        var width = (int)font.GetStyleTag(StyleTag.WIDTH);
                        string isItalic = (byte)font.GetStyleTag(StyleTag.ITALIC) == 1 ? "italic, " : "";
                        var slant = (int)font.GetStyleTag(StyleTag.SLANT_ANGLE);

                        //Debug.Log($"{fontReference} (weight {weight}, width {width}, {isItalic}slant {slant})");
                    }
                    face.Dispose();
                }
                blob.Dispose();
                return true;
            }
            else
            {
                Debug.LogWarning("Ensure you only have files ending with 'ttf' or 'otf' (case insensitiv) in font list");
                return false;
            }
        }

        struct GetFontMetadataJob : IJob
        {
            [ReadOnly] public NativeArray<FixedString512Bytes> systemFonts;
            public NativeList<FontReference> fontReferences;

            public void Execute()
            {
                var language = new Language(Harfbuzz.HB_TAG('E', 'N', 'G', ' '));
                for (int i = 0, ii = systemFonts.Length; i < ii; i++)
                    GetFontInfo(systemFonts[i].ToString(), language, fontReferences);
            }
        }
        public struct FontReference : IEquatable<FontReference>
        {
            public FixedString512Bytes filePath;
            public int faceIndex; //ignore in equality test as it does not matter if index font file on target device and developer machine is the same
            public FixedString128Bytes fontFamily;
            public FixedString128Bytes fontSubFamily;
            public FixedString128Bytes typographicFamily;
            public FixedString128Bytes typographicSubfamily;

            public override bool Equals(object obj)
            {
                if (obj is FontReference item)
                {
                    return Equals(item);
                }
                return false;
            }
            bool IEquatable<FontReference>.Equals(FontReference other)
            {
                return fontFamily == other.fontFamily &&
                       fontSubFamily == other.fontSubFamily &&
                       typographicFamily == other.typographicFamily &&
                       typographicSubfamily == other.typographicSubfamily;
            }
            public override int GetHashCode()
            {
                unchecked
                {
                    int hc = -1817952719;
                    hc = (-1521134295) * hc + fontFamily.GetHashCode();
                    hc = (-1521134295) * hc + fontSubFamily.GetHashCode();
                    hc = (-1521134295) * hc + typographicFamily.GetHashCode();
                    hc = (-1521134295) * hc + typographicSubfamily.GetHashCode();
                    return hc;
                }
            }

            public static bool operator ==(FontReference target, FontReference other) { return target.Equals(other); }
            public static bool operator !=(FontReference target, FontReference other) { return !target.Equals(other); }
            public override string ToString()
            {
                if(typographicFamily != "")
                    return $"{fontFamily} - {fontSubFamily} (typographic: {typographicFamily} - {typographicSubfamily})";
                else
                    return $"{fontFamily} - {fontSubFamily}";
            }
        }
    }
}