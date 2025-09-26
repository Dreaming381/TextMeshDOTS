using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Profiling;
using Unity.Entities;
using UnityEngine.TextCore;
using UnityEngine;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.HarfBuzz.Bitmap;
using Unity.Collections.LowLevel.Unsafe;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct UpdateSDFAtlasTextureJob : IJobParallelForDefer
    {
        [NativeDisableParallelForRestriction] public NativeArray<byte> textureData;

        public Entity fontEntity;
        [ReadOnly] public FontTable fontTable;
        [ReadOnly] public GlyphTable glyphTable;
        [ReadOnly] public ComponentLookup<FontAssetMetadata> fontAssetMetadataLookup; //temporary link between Font Entities and FontTable
        [ReadOnly] public NativeList<GlyphTable.Key> placedGlyphs;
        [ReadOnly] public ComponentLookup<AtlasData> atlasDataLookup;
        [ReadOnly] public ComponentLookup<DrawAndPaintFunctions> drawAndPaintFunctionsLookup;
        [ReadOnly] public BufferLookup<UsedGlyphs> usedGlyphsBuffer;
        [ReadOnly] public BufferLookup<UsedGlyphRects> usedGlyphRectsBuffer;

        [NativeSetThreadIndex]
        int threadIndex;

        public ProfilerMarker marker;
        public void Execute(int i)
        {
            var atlasData = atlasDataLookup[fontEntity];
            var drawAndPaintFunctions = drawAndPaintFunctionsLookup[fontEntity];
            var usedGlyphs = usedGlyphsBuffer[fontEntity].Reinterpret<uint>();
            var usedGlyphRects = usedGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();

            var glyphID = glyphTable.glyphHashToIdMap[placedGlyphs[i]];
            var glyphEntry = glyphTable.GetEntry(glyphID);

            if (glyphEntry.width == 0 && glyphEntry.height == 0)
                return;//glyph has no size, nothing needs to be renderered/added to texture

            var fontAssetMetaData = fontAssetMetadataLookup[fontEntity];
            var face = fontTable.faces[fontAssetMetaData.faceIndex];
            var font = fontTable.GetOrCreateFont(fontAssetMetaData.faceIndex, threadIndex);
            var samplingSize = FontTextureSize.Normal.GetSamplingSize();
            font.SetScale(samplingSize, samplingSize);

            var maxDeviation = BezierMath.GetMaxDeviation(font.GetScale().x);

            var drawData = new DrawData(256, 16, maxDeviation, Allocator.Temp);
            marker.Begin();
            uint glyphIndex = glyphEntry.key.glyphIndex;
            font.DrawGlyph(glyphIndex, drawAndPaintFunctions.drawFunctions, ref drawData);

            var usedGlyphIndex = usedGlyphs.Reinterpret<uint>().AsNativeArray().IndexOf(glyphIndex);
            if (usedGlyphIndex != -1)
            {
                //render SDF into the reserved padded atlas texture  window 
                var atlasRect = usedGlyphRects[usedGlyphIndex];
                //Debug.Log($"old: {atlasRect.x} {atlasRect.y} {atlasRect.width} {atlasRect.height}");
                //BezierMath.SplitCuvesToLines(ref drawData, maxDeviation, out DrawData flatenedDrawData);
                SDF_SPMD.SDFGenerateSubDivisionLineEdges(face.sdfOrientation, 
                                                         ref drawData, 
                                                         textureData, 
                                                         atlasRect, 
                                                         atlasData.padding, 
                                                         atlasData.atlasWidth,
                                                         atlasData.atlasHeight, 
                                                         atlasData.padding);
            }
            else
                Debug.Log($"{glyphEntry.key.glyphIndex} not found {usedGlyphs.Length}");
            marker.End();
        }
    }    
}