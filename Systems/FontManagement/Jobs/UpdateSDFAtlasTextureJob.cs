using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Profiling;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.TextCore;
using UnityEngine;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.HarfBuzz.SDF;

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

            var font = nativeFontPointer.font;
            var drawData = new DrawData(256, 16, Allocator.Temp);
            marker.Begin();
            font.DrawGlyph(glyphBlob.glyphID, nativeFontPointer.drawFunctions, ref drawData);            

            //shift the bezier edges so that they are in the center of the reserved atlas padded texture window (usedRects)
			var edges = drawData.edges;
            var shiftx = drawData.glyphRect.min.x - ((glyphBlob.glyphExtents.width + 2 * atlasData.padding - drawData.glyphRect.width) / 2);
            var shifty = drawData.glyphRect.min.y - ((glyphBlob.glyphExtents.height + 2 * atlasData.padding - drawData.glyphRect.height) / 2);
            float2 shift = -new float2(shiftx, shifty);
            for (int k = 0, kk = edges.Length; k < kk; k++)
            {
                ref var edge = ref edges.ElementAt(k);
                edge.start_pos += shift;
                edge.end_pos += shift;
                edge.control1 += shift;
                edge.control2 += shift;
                //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
            }
            var glyphIndex = usedGlyphs.Reinterpret<uint>().AsNativeArray().IndexOf(glyphBlob.glyphID);
            if (glyphIndex != -1)
            {
                var atlasRect = usedGlyphRects[glyphIndex]; //render SDF into the reserved padded atlas texture  window 
                SDF.SDFGenerateSubDivision(nativeFontPointer.orientation, ref drawData, textureData, atlasRect, atlasData.atlasWidth, atlasData.atlasHeight);
            }
            else
                Debug.Log($"{glyphBlob.glyphID} not found {usedGlyphs.Length}");
            marker.End();
        }
    }    
}