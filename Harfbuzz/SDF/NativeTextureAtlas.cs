using Unity.Collections;
using UnityEngine.TextCore;

namespace HarfBuzz
{

    public struct NativeTextureAtlas
    {
        public NativeHashMap<uint, GlyphRect> usedGlyphRects;
        public NativeArray<byte> textureData;
        public int textureWidth;
        public int textureHeight;
        int xPos;
        int yPos;
        int largestHThisRow;
        public NativeTextureAtlas(NativeArray<byte> m_textureData, int glyphCapacity, int atlasWidth, int atlasheight, Allocator allocator)
        {
            usedGlyphRects = new NativeHashMap<uint, GlyphRect>(glyphCapacity, allocator);
            textureWidth = atlasWidth;
            textureHeight = atlasheight;
            textureData = m_textureData;
            xPos = 0;
            yPos = 0;
            largestHThisRow = 0;
        }
        public bool TryAddGlyph(uint glyphID, ref GlyphRect glyphRect)
        {
            // If this rectangle will go past the width of the image
            // Then loop around to next row, using the largest height from the previous row
            if ((xPos + glyphRect.width) > textureWidth)
            {
                yPos += largestHThisRow;
                xPos = 0;
                largestHThisRow = 0;
            }

            // If we go off the bottom edge of the image, then we've failed
            if ((yPos + glyphRect.height) > textureHeight)
                return false;

            // This is the position of the rectangle
            glyphRect.x = xPos;
            glyphRect.y = yPos;

            // Move along to the next spot in the row
            xPos += glyphRect.width;

            // Just saving the largest height in the new row
            if (glyphRect.height > largestHThisRow)
                largestHThisRow = glyphRect.height;

            // Success!
            usedGlyphRects.Add(glyphID, glyphRect);
            //rect.wasPacked = true;
            return true;
        }

        //public bool TryAddGlyphs(NativeList<GlyphRect> newGlyphRects)
        //{
        //    bool success = false;
        //    // Sort by a heuristic
        //    newGlyphRects.Sort(default(GlyphRectHeightComparer));            

        //    // Loop over all the rectangles
        //    for (int i = 0, length= newGlyphRects.Length; i < length; i++) 
        //    {
        //        var glyphRect = newGlyphRects[i];
        //        success = TryAddGlyph(glyphRect, out glyphRect);
        //        if (!success)
        //            break;
        //    }
        //    return success;
        //}
        //public struct GlyphRectHeightComparer : IComparer<GlyphRect>
        //{
        //    public int Compare(GlyphRect a, GlyphRect b)
        //    {
        //        if (a.height == b.height)
        //        {
        //            return 0;
        //        }
        //        else
        //        {
        //            if (a.height > b.height)
        //                return 1;
        //            else
        //                return -1;
        //        }
        //    }
        //}
        //public struct HBGlyph
        //{
        //    public uint glyphID;
        //    GlyphRect glyphRect;
        //}
        public void Dispose()
        {
            if (usedGlyphRects.IsCreated) usedGlyphRects.Dispose();
            //do not dispose textureData as the owner is Texture2D
        }
    }
}
