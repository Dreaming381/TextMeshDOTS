using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    /// <summary>
    /// textureData and clipRect (informing about texture width and height) are the ultimate output of the paint API
    /// use this output to blit it into target texture (atlas) at desired location
    /// </summary>     
    public struct PaintData : IDisposable
    {
        public DrawDelegates drawDelegates;
        public DrawData clipGlyph;

        internal FixedStack512Bytes<float2x3> transformStack; //could also use Unity AffineTransform (but this would require use of float3 vs float2)
        public uint color;
  
        public BBox clipRect;
        public NativeArray<ColorARGB> textureData;
        public HB_PAINT_IMAGE_FORMAT imageFormat;
        public Blob imageBlob;


        public PaintData(DrawDelegates drawDelegates, int edgeCapacity, int contourCapacity, Allocator allocator)
        {
            this.drawDelegates = drawDelegates;
            clipGlyph = new DrawData(edgeCapacity, contourCapacity, allocator);
            imageFormat = default;
            imageBlob = default;
            clipRect = BBox.Empty;
            transformStack = new();
            //transformStack.Add(AffineTransform.identity);
            transformStack.Add(new float2x3
            {
                c0 = new float2(1, 0),  // xx, yx
                c1 = new float2(0, 1),  // xy, yy
                c2 = new float2(0, 0)   // x0, y0
            });

            color = default;
            this.textureData = default;
        }
        public void Clear()
        {
            clipGlyph.Clear();
            imageFormat = default;
            imageBlob = default;
            clipRect = BBox.Empty;
            transformStack.Clear();
            //transformStack.Add(AffineTransform.identity);
            transformStack.Add(new float2x3
            {
                c0 = new float2(1, 0),  // xx, yx
                c1 = new float2(0, 1),  // xy, yy
                c2 = new float2(0, 0)   // x0, y0
            });
        }
        public void Dispose()
        {
            imageBlob.Dispose();
        }
    }

    
}