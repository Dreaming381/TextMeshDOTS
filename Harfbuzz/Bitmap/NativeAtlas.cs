using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using UnityEngine.TextCore;
using Unity.Entities;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{    
    public static class NativeAtlas
    {
        public static void InitializeFreeGlyphRects(ref DynamicBuffer<FreeGlyphRects> freeGlyphRectsBuffer, int atlasWidth, int atlasHeight)
        {
            freeGlyphRectsBuffer.Clear();
            freeGlyphRectsBuffer.Reinterpret<GlyphRect>().Add(new GlyphRect(0, 0, atlasWidth, atlasHeight));
        }

        public static bool AddGlyphs(
            int padding,
            NativeList<GlyphBlob> glyphsToPlace,
            NativeList<GlyphBlob> placedGlyphs,
            DynamicBuffer<uint> usedGlyphs,
            DynamicBuffer<GlyphRect> usedRects,
            DynamicBuffer<GlyphRect> freeRects)
        {
            //Walk all rectsToPlace and find the a free rect that fits best given 
            //our current freeRect list. Then start again.
            //Sorting improves nothing, algorithm always finds best glyph to place
            var doublePadding = 2 * padding;
            while (glyphsToPlace.Length > 0)
            {                
                int bestShortSideScore = int.MaxValue;
                int bestLongSideScore = int.MaxValue;
                int bestGlyphID = 0;
                GlyphRect bestRect = default;
                for (int i = 0, length= glyphsToPlace.Length; i<length; i++)
                {
                    var glyphExtents = glyphsToPlace[i].glyphExtents;
                    int shortSideScore = int.MaxValue;
                    int longSideScore = int.MaxValue;

                    var idealRect = FindIdealRect(glyphExtents.width + doublePadding, glyphExtents.height + doublePadding, freeRects, ref shortSideScore, ref longSideScore);

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
                    var currentGlyph = glyphsToPlace[bestGlyphID];
                    var glyphExtents =currentGlyph.glyphExtents;
                    usedRects.Add(bestRect);
                    usedGlyphs.Add(currentGlyph.glyphID);
                    
                    //currentGlyph.glyphRect = bestRect; //bestRect is the padded atlas texture window.
                    //the glyph (bounded by glyphExtents) will be renderered into the center of this window
                    //GlyphRect needs to point to non-padded Glyph, and NOT to the entire padded atlas texture window
                    currentGlyph.glyphRect = new GlyphRect
                    {
                        x = bestRect.x + padding,
                        y = bestRect.y + padding,
                        width = glyphExtents.width,
                        height = glyphExtents.height
                    };
                    
                    placedGlyphs.Add(currentGlyph);
                    glyphsToPlace.RemoveAt(bestGlyphID);
                }
                else
                {
                    return false;//atlas full; no room left
                }
            }
            return true;
        }
        public static bool TryAddGlyph(
            int padding,
            GlyphBlob glyphToPlace,
            out GlyphBlob placedGlyph,
            DynamicBuffer<uint> glyphsInUse,            
            DynamicBuffer<GlyphRect> usedRects,
            DynamicBuffer<GlyphRect> freeRects)
        {
            var doublePadding = 2 * padding;
            int shortSideScore = int.MaxValue;
            int longSideScore = int.MaxValue;

            //var glyphRect = glyph.atlasRect;
            var glyphExtents = glyphToPlace.glyphExtents;
            var idealRect = FindIdealRect(glyphExtents.width + doublePadding, glyphExtents.height + doublePadding, freeRects, ref shortSideScore, ref longSideScore);
            if (idealRect.width > 0 && idealRect.height > 0)
            {
                //usedRects.Add(idealRect);
                //return true;
                RemoveRectFromFreeList(idealRect, freeRects);
                usedRects.Add(idealRect);
                glyphsInUse.Add(glyphToPlace.glyphID);

                //currentGlyph.glyphRect = bestRect; //bestRect is the padded atlast Texture windows.
                //the glyph (dounded by glyphExtents) will be renderered into the center of this windows
                //GlyphRect needs to point to non-padded Glyph, and NOT to the entire padded atlas texture window
                glyphToPlace.glyphRect = new GlyphRect
                {
                    x = idealRect.x + padding,
                    y = idealRect.y + padding,
                    width = glyphExtents.width,
                    height = glyphExtents.height
                };
                placedGlyph = glyphToPlace;
                return true;
            }
            else
            {
                Debug.Log($"Ran out of Space: glyph {glyphExtents} could not be placed");
                placedGlyph = default;
                return false; //no room left
            }
        }

        private static GlyphRect FindIdealRect(int width, int height, DynamicBuffer<GlyphRect> freeRects, ref int bestShortSideFit, ref int bestLongSideFit)
        {
            GlyphRect bestNode =default;
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
                        bestNode = new GlyphRect(freeRects[i].x, freeRects[i].y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }
            return bestNode;
        }

        //remove a rect area from the freeRect list
        private static void RemoveRectFromFreeList(GlyphRect rectToRemove, DynamicBuffer<GlyphRect> freeRects)
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
    };
    static class RectExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsContainedIn(this GlyphRect a, GlyphRect b)
        {
            return a.x >= b.x && a.y >= b.y
                && a.x + a.width <= b.x + b.width
                && a.y + a.height <= b.y + b.height;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Overlaps(this GlyphRect a, GlyphRect b)
        {
            var a_xMax = a.x + a.width;
            var b_xMax = b.x + b.width;
            var a_yMax = a.y + a.height;
            var b_yMax = b.y + b.height;
            return b.x < a_xMax && b_xMax > a.x && b.y < a_yMax && b_yMax > a.y;
        }
    }
}