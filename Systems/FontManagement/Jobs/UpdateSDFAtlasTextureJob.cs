using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Profiling;
using Unity.Entities;
using UnityEngine.TextCore;
using UnityEngine;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.HarfBuzz.Bitmap;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct UpdateSDFAtlasTextureJob : IJobParallelForDefer
    {
        [NativeDisableParallelForRestriction] public NativeArray<byte> textureData;

        public Entity fontEntity;
        [ReadOnly] public NativeList<GlyphBlob> placedGlyphs;
        [ReadOnly] public ComponentLookup<AtlasData> atlasDataLookup;
        [ReadOnly] public ComponentLookup<NativeFontPointer> nativeFontPointerLookup;
        [ReadOnly] public BufferLookup<UsedGlyphs> usedGlyphsBuffer;
        [ReadOnly] public BufferLookup<UsedGlyphRects> usedGlyphRectsBuffer;        
        

        public ProfilerMarker marker;
        public void Execute(int i)
        {
            var atlasData = atlasDataLookup[fontEntity];
            var nativeFontPointer = nativeFontPointerLookup[fontEntity];
            var usedGlyphs = usedGlyphsBuffer[fontEntity].Reinterpret<uint>();
            var usedGlyphRects = usedGlyphRectsBuffer[fontEntity].Reinterpret<GlyphRect>();

            var glyphBlob = placedGlyphs[i];
            if (glyphBlob.glyphExtents.width == 0 && glyphBlob.glyphExtents.height == 0)
                return;//glyph has no size, nothing needs to be renderered/added to texture

            var font = nativeFontPointer.font;
            var maxDeviation = BezierMath.GetMaxDeviation(font.GetScale().x);

            var drawData = new DrawData(256, 16, maxDeviation, Allocator.Temp);
            marker.Begin();
            font.DrawGlyph(glyphBlob.glyphID, nativeFontPointer.drawFunctions, ref drawData);

            var glyphIndex = usedGlyphs.Reinterpret<uint>().AsNativeArray().IndexOf(glyphBlob.glyphID);
            if (glyphIndex != -1)
            {
                //render SDF into the reserved padded atlas texture  window 
                var atlasRect = usedGlyphRects[glyphIndex];
                //BezierMath.SplitCuvesToLines(ref drawData, maxDeviation, out DrawData flatenedDrawData);
                SDF_SPMD.SDFGenerateSubDivisionLineEdges(nativeFontPointer.orientation, ref drawData, textureData, atlasRect, atlasData.padding, atlasData.atlasWidth, atlasData.atlasHeight, atlasData.padding);
            }
            else
                Debug.Log($"{glyphBlob.glyphID} not found {usedGlyphs.Length}");
            marker.End();
        }
    }    
}