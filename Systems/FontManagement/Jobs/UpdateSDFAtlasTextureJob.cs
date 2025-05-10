using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Profiling;
using Unity.Entities;
using UnityEngine.TextCore;
using UnityEngine;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.HarfBuzz.Bitmap;
using Font = TextMeshDOTS.HarfBuzz.Font;
using Unity.Collections.LowLevel.Unsafe;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct UpdateSDFAtlasTextureJob : IJobParallelForDefer
    {
        [NativeDisableParallelForRestriction] public NativeArray<byte> textureData;

        public Entity fontEntity;
        [ReadOnly] public FontTable fontTable;
        [ReadOnly] public ComponentLookup<FontAssetMetadata> fontAssetMetadataLookup; //temporary link between Font Entities and FontTable
        [ReadOnly] public NativeList<GlyphBlob> placedGlyphs;
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

            var glyphBlob = placedGlyphs[i];
            if (glyphBlob.glyphExtents.width == 0 && glyphBlob.glyphExtents.height == 0)
                return;//glyph has no size, nothing needs to be renderered/added to texture

            var fontAssetMetaData = fontAssetMetadataLookup[fontEntity];
            var faceEntry = fontTable.faceEntries[fontAssetMetaData.faceIndex];
            var fontPtr = fontTable.GetOrCreateFont(fontAssetMetaData.faceIndex, threadIndex);
            var samplingSize = FontTextureSize.Normal.GetSamplingSize();
            Harfbuzz.hb_font_set_scale(fontPtr, samplingSize, samplingSize);
            Font font = default;
            font.ptr = fontPtr;

            var maxDeviation = BezierMath.GetMaxDeviation(font.GetScale().x);

            var drawData = new DrawData(256, 16, maxDeviation, Allocator.Temp);
            marker.Begin();
            font.DrawGlyph(glyphBlob.glyphID, drawAndPaintFunctions.drawFunctions, ref drawData);

            var glyphIndex = usedGlyphs.Reinterpret<uint>().AsNativeArray().IndexOf(glyphBlob.glyphID);
            if (glyphIndex != -1)
            {
                //render SDF into the reserved padded atlas texture  window 
                var atlasRect = usedGlyphRects[glyphIndex];
                //BezierMath.SplitCuvesToLines(ref drawData, maxDeviation, out DrawData flatenedDrawData);
                SDF_SPMD.SDFGenerateSubDivisionLineEdges(faceEntry.sdfOrientation, ref drawData, textureData, atlasRect, atlasData.padding, atlasData.atlasWidth, atlasData.atlasHeight, atlasData.padding);
            }
            else
                Debug.Log($"{glyphBlob.glyphID} not found {usedGlyphs.Length}");
            marker.End();
        }
    }    
}