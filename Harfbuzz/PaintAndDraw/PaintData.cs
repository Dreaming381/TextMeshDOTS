using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    /// <summary>
    /// textureData and clipRect (informing about texture width and height) are the ultimate output of the paint API
    /// use this output to blit it into target texture (atlas) at desired location
    /// </summary>     
    public struct PaintData
    {
        public DrawDelegates drawDelegates;
        public DrawData clipGlyph;
        public uint glyphID;
        internal FixedStack512Bytes<float2x3> transformStack; //could also use Unity AffineTransform (but this would require use of float3 vs float2)
        public uint color;  
        public BBox clipRect;
        public NativeArray<ColorARGB> textureData;
        public NativeArray<byte> imageData;
        public HB_PAINT_IMAGE_FORMAT imageFormat;
        public int imageWidth;
        public int imageHeight;

        public PaintData(DrawDelegates drawDelegates, int edgeCapacity, int contourCapacity, Allocator allocator)
        {
            this.drawDelegates = drawDelegates;
            clipGlyph = new DrawData(edgeCapacity, contourCapacity, allocator);
            glyphID = default;
            transformStack = new();
            transformStack.Add(PaintUtils.AffinityTransformIdentity);
            color = default;
            clipRect = BBox.Empty;
            textureData = default;
            imageData = default;
            imageFormat = default;
            imageWidth = -1;
            imageHeight = -1; 
            
            imageData = default;
        }
        public void Clear()
        {
            clipGlyph.Clear();
            glyphID = default;
            transformStack.Clear();
            transformStack.Add(PaintUtils.AffinityTransformIdentity);
            color = default;
            clipRect = BBox.Empty;
            textureData = default;
            imageData = default;
            imageFormat = default;
            imageWidth = -1;
            imageHeight = -1;            
        }
    }    
}