using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace HarfBuzz
{    
    public struct NativeAtlas
    {
        public int atlasWidth;
        public int atlasHeight;
        public NativeHashMap<uint, RectInt> usedRects;
        public NativeList<RectInt> freeRects;
        public NativeArray<byte> textureData;

        public NativeAtlas(NativeArray<byte> m_textureData, int glyphCapacity, int atlasWidth, int atlasHeight, Allocator allocator)
        {
            this.atlasWidth = atlasWidth;
            this.atlasHeight = atlasHeight;
            usedRects = new NativeHashMap<uint, RectInt>(glyphCapacity, allocator);
            freeRects = new NativeList<RectInt>(glyphCapacity, allocator)
            {
                new RectInt(0, 0, atlasWidth, atlasHeight)
            };
            textureData = m_textureData;
        }

        public static void AddGlyphs(NativeList<HBGlyph> glyphsToPlace, NativeList<uint> placedGlyphs, NativeList<RectInt> freeRects, NativeHashMap<uint, RectInt> usedRects)
        {
            //Walk all rectsToPlace and find the one that fits the best given 
            //our current freeRect list. Then start again
            //sorting improves nothing, algorithm always finds best glyph to place
            //glyphsToPlace.Sort(default(GlyphSizeComparer)); 

            while (glyphsToPlace.Length > 0)
            {                
                int bestShortSideScore = int.MaxValue;
                int bestLongSideScore = int.MaxValue;
                int bestGlyphID = 0;
                RectInt bestRect = default;
                for (int i = 0, length= glyphsToPlace.Length; i<length; i++)
                {
                    var currentGlyph = glyphsToPlace[i];
                    var currentRect = currentGlyph.glyphRect;
                    int shortSideScore = int.MaxValue;
                    int longSideScore = int.MaxValue;

                    var idealRect = FindIdealRect(currentRect.width, currentRect.height, freeRects, ref shortSideScore, ref longSideScore);

                    if (shortSideScore < bestShortSideScore || (shortSideScore == bestShortSideScore && longSideScore < bestLongSideScore))
                    {
                        bestShortSideScore = shortSideScore;
                        bestLongSideScore = longSideScore;
                        bestGlyphID = i;
                        bestRect = idealRect;
                    }
                }

                if (bestRect.width > 0 && bestRect.height > 0)
                {
                    RemoveRectFromFreeList(bestRect, freeRects);
                    var glyphID = glyphsToPlace[bestGlyphID].glyphID;
                    usedRects.Add(glyphID, bestRect);
                    placedGlyphs.Add(glyphID);
                    glyphsToPlace.RemoveAt(bestGlyphID);
                    
                }
                else
                {
                    Debug.Log($"Ran out of Space: glyph {glyphsToPlace.Length} glyphs could not be placed");
                    break; //atlas full; no room left
                }
            }
        }
        public static bool TryAddGlyph(HBGlyph glyph, NativeList<RectInt> freeRects, NativeHashMap<uint, RectInt> usedRects, out RectInt outRect)
        {
            int shortSideScore = int.MaxValue;
            int longSideScore = int.MaxValue;

            var glyphRect = glyph.glyphRect;
            outRect = FindIdealRect((int)glyphRect.width, (int)glyphRect.height, freeRects, ref shortSideScore, ref longSideScore);
            if (outRect.width > 0 && outRect.height > 0)
            {
                RemoveRectFromFreeList(outRect, freeRects);
                usedRects.Add(glyph.glyphID, outRect);
                return true;
            }
            else
            {
                Debug.Log($"Ran out of Space: 1 glyph could not be placed");
                return false; //no room left
            }
        }

        private static RectInt FindIdealRect(int width, int height, NativeList<RectInt> freeRects, ref int bestShortSideFit, ref int bestLongSideFit)
        {
            RectInt bestNode =default;
            for (int i = 0; i < freeRects.Length; ++i)
            {
                if (freeRects[i].width >= width && freeRects[i].height >= height)
                {
                    int remainingX = (int)(freeRects[i].width - width);
                    int remainingY = (int)(freeRects[i].height - height);

                    int shortSideFit = math.min(remainingX, remainingY);
                    int longSideFit = math.max(remainingX, remainingY);

                    if (shortSideFit < bestShortSideFit || (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                    {
                        bestNode = new RectInt(freeRects[i].x, freeRects[i].y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }
            return bestNode;
        }

        //remove a rect area from the freeRect list
        private static void RemoveRectFromFreeList(RectInt rectToRemove, NativeList<RectInt> freeRects)
        {
            for (int i = 0; i < freeRects.Length; ++i)
            {
                var freeRect = freeRects[i];
                if (freeRect.Overlaps(rectToRemove))
                {
                    if (rectToRemove.x < freeRect.x + freeRect.width && rectToRemove.x + rectToRemove.width > freeRect.x)
                    {
                        // New node at the top side of the used node.
                        if (rectToRemove.y > freeRect.y && rectToRemove.y < freeRect.y + freeRect.height)
                        {
                            var newNode = freeRect;
                            newNode.height = rectToRemove.y - newNode.y;
                            freeRects.Add(newNode);
                        }

                        // New node at the bottom side of the used node.
                        if (rectToRemove.y + rectToRemove.height < freeRect.y + freeRect.height)
                        {
                            var newNode = freeRect;
                            newNode.y = rectToRemove.y + rectToRemove.height;
                            newNode.height = freeRect.y + freeRect.height - (rectToRemove.y + rectToRemove.height);
                            freeRects.Add(newNode);
                        }
                    }

                    if (rectToRemove.y < freeRect.y + freeRect.height && rectToRemove.y + rectToRemove.height > freeRect.y)
                    {
                        // New node at the left side of the used node.
                        if (rectToRemove.x > freeRect.x && rectToRemove.x < freeRect.x + freeRect.width)
                        {
                            var newNode = freeRect;
                            newNode.width = rectToRemove.x - newNode.x;
                            freeRects.Add(newNode);
                        }

                        // New node at the right side of the used node.
                        if (rectToRemove.x + rectToRemove.width < freeRect.x + freeRect.width)
                        {
                            var newNode = freeRect;
                            newNode.x = rectToRemove.x + rectToRemove.width;
                            newNode.width = freeRect.x + freeRect.width - (rectToRemove.x + rectToRemove.width);
                            freeRects.Add(newNode);
                        }
                    }

                    freeRects.RemoveAt(i--);
                }
            }

            //remove free rects that are wholly contained by others

            for (int i = 0; i < freeRects.Length; ++i)
            {
                for (int j = i + 1; j < freeRects.Length; ++j)
                {
                    if (freeRects[i].IsContainedIn(freeRects[j]))
                    {
                        freeRects.RemoveAt(i);
                        --i;
                        break;
                    }

                    if (freeRects[j].IsContainedIn(freeRects[i]))
                    {
                        freeRects.RemoveAt(j);
                        --j;
                    }
                }
            }
        }
        public void Dispose()
        {
            if (freeRects.IsCreated) freeRects.Dispose();
            if (usedRects.IsCreated) usedRects.Dispose();
            //do not dispose textureData as the owner is Texture2D
        }
    };



    static class RectExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsContainedIn(this RectInt a, RectInt b)
        {
            return a.x >= b.x && a.y >= b.y
                && a.x + a.width <= b.x + b.width
                && a.y + a.height <= b.y + b.height;
        }
    }
}