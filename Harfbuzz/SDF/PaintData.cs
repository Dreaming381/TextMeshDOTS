using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    public struct PaintData : IDisposable
    {
        public int width;
        public int height;
        public DrawDelegates drawDelegates;
        public DrawData clipGlyph;
        public BBox clipRect;
        internal FixedStack512Bytes<float2x3> transformStack;
        //internal FixedStack512Bytes<AffineTransform> transformStack;
        public uint color;
        public NativeArray<ColorARGB> textureData;

        public HB_PAINT_IMAGE_FORMAT imageFormat;
        public Blob imageBlob;

        public PaintData(DrawDelegates drawDelegates, NativeArray<ColorARGB> textureData, int width, int height, int edgeCapacity, int contourCapacity, Allocator allocator)
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
            this.textureData = textureData;
            this.width = width;
            this.height = height;
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