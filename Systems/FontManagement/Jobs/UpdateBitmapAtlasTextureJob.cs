using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Profiling;
using Unity.Entities;
using UnityEngine.TextCore;
using UnityEngine;
using TextMeshDOTS.HarfBuzz;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct UpdateBitmapAtlasTextureJob : IJobParallelForDefer
    {
        [NativeDisableParallelForRestriction] public NativeArray<ColorARGB> textureData;

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
            if (glyphBlob.glyphExtents.width == 0 && glyphBlob.glyphExtents.height ==0)
                return;//glyph has no size, nothing needs to be renderered/added to texture

            var font = nativeFontPointer.font;
            var maxDeviation = BezierMath.GetMaxDeviation(font.GetScale().x);
            var paintData = new PaintData(nativeFontPointer.drawFunctions, 256, 4, maxDeviation, Allocator.Temp);
            marker.Begin();
            font.PaintGlyph(glyphBlob.glyphID, ref paintData, nativeFontPointer.paintFunctions, 0, new ColorARGB(255, 0, 0, 0));

            var glyphIndex = usedGlyphs.Reinterpret<uint>().AsNativeArray().IndexOf(glyphBlob.glyphID);
            if (glyphIndex != -1)
            {
                var atlasRect = usedGlyphRects[glyphIndex]; //render Bitmap into the reserved padded atlas texture  window 
                if (paintData.imageData.Length > 0)//render PNG and SVG
                {
                    //not implemented due to managed code
                    //if (paintData.imageFormat == HB_PAINT_IMAGE_FORMAT.PNG)
                    //{
                    //    var png = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    //    png.LoadImage(paintData.imageData.ToArray());
                    //    var sourceTexture = png.GetRawTextureData<ColorARGB>();//returned texture format will actually be RGBA (Graphicsformat 87), so convert to ARGB                    
                    //    PaintUtils.BlitRawTexture(sourceTexture, paintData.imageWidth, paintData.imageHeight, textureData, atlasWidth, atlasHeight, 0, 0);
                    //}
                    //if (paintData.imageFormat == HB_PAINT_IMAGE_FORMAT.SVG)
                    //{
                    //    //consider use of com.unity.vectorgraphics (which designed to render svg)
                    //}
                }
                else if (paintData.paintSurface.Length > 0) // render COLR, raw BRGA data stored in sbix, CBDT
                {
                    var clipRect = paintData.clipRect;
                    //var glyphExtents = glyphBlob.glyphExtents;
                    //if (glyphExtents.width != clipRect.intWidth || glyphExtents.height != clipRect.intHeight)
                    //    Debug.LogWarning($"Dimensions of glyphRect reserved in atlas ({glyphExtents.width},{glyphExtents.height}) and painted GlyphRect ({clipRect.intWidth},{clipRect.intHeight}) do not match");
                    PaintUtils.BlitRawTexture(paintData.paintSurface, clipRect.intWidth, clipRect.intHeight, textureData, atlasData.atlasWidth, atlasData.atlasHeight, atlasRect.x, atlasRect.y);
                }
            }
            else
                Debug.Log($"{glyphBlob.glyphID} not found {usedGlyphs.Length}");            

            marker.End();
        }
    }    
}