using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.TextCore.Text;
using TextMeshDOTS.HarfBuzz;
using Unity.Burst.CompilerServices;
using UnityEngine;


namespace TextMeshDOTS
{
    
    internal static class GlyphGeneration
    {
        /// <summary> This function logic follows TMPro_Private.GenerateTextMesh() </summary>
        internal static unsafe void CreateRenderGlyphs(ref FontAssetArray fontAssetArray,
                                                        NativeHashMap<FontAssetRef, Entity> fontEntities,
                                                       in ComponentLookup<DynamicFontAssets> dynamicFontAssetsLookup,
                                                       ref DynamicBuffer<FontMaterialSelectorForGlyph> m_selectorBuffer,
                                                       ref DynamicBuffer<RenderGlyph> renderGlyphs,
                                                       ref GlyphMappingWriter mappingWriter,                                                       
                                                       in DynamicBuffer<CalliByte> calliBytes,
                                                       in DynamicBuffer<GlyphOTF> glyphOTFs,
                                                       in DynamicBuffer<TextSpan> textSpans,
                                                       in TextBaseConfiguration baseConfiguration,
                                                       bool multiFont)
        {
            //Debug.Log("CreateRenderGlyphs");
            renderGlyphs.Clear();
            var fontAssetRefs = fontAssetArray.fontAssetRefs;
            int textSpanCounter = 0;
            var currentTextSpan = textSpans[textSpanCounter++];
            if (calliBytes.IsEmpty)
            {
                
                return;
            }
            var calliString = new CalliString(calliBytes);
            var characters = calliString.GetEnumerator();             

            int characterCount = 0;
            int lastWordStartCharacterGlyphIndex = 0;
            FixedList512Bytes<int> characterGlyphIndicesWithPreceedingSpacesInLine = default;
            int accumulatedSpaces = 0;
            int startOfLineGlyphIndex = 0;
            int lastCommittedStartOfLineGlyphIndex = -1;
            int lineCount = 0;
            bool isLineStart = true;
            float currentLineHeight = 0f;
            float ascentLineDelta = 0;
            float decentLineDelta = 0;
            float accumulatedVerticalOffset = 0f;
            float maxLineAscender = float.MinValue;
            float maxLineDescender = float.MaxValue;
            float xAdvance = 0f;

            var previousFontMaterialIndex = currentTextSpan.fontMaterialIndex;
            var currentFontAssetRef = fontAssetRefs[currentTextSpan.fontMaterialIndex];
            var currentFontEntity = fontEntities[currentFontAssetRef];
            ref DynamicFontBlob currentFont = ref dynamicFontAssetsLookup[currentFontEntity].blob.Value;

            #region AtlasFactor
            // Unity scales native Opentype metrics in GlyphRect and GlyphMetrics by 
            // (unit * atlasSamplingPointSize / Opentype.xScale)
            // So raw values from Opentype font need to be scaled like that as well.
            var scaledDynamicFont = new ScaledDynamicFont(ref currentFont, out float xNativeToUnity, out float yNativeToUnity);
            #endregion


            // Calculate the scale of the font based on selected font size and sampling point size.
            // baseScale is calculated using the font asset assigned to the text object.            
            float baseScale = baseConfiguration.fontSize / currentFont.atlasSamplingPointSize * (baseConfiguration.isOrthographic ? 1 : 0.1f);
            float currentElementScale = baseScale;
            float currentEmScale = baseConfiguration.fontSize * 0.01f * (baseConfiguration.isOrthographic ? 1 : 0.1f);

            float topAnchor = GetTopAnchorForConfig(ref scaledDynamicFont, baseConfiguration.verticalAlignment, baseScale);
            float bottomAnchor = GetBottomAnchorForConfig(ref scaledDynamicFont, baseConfiguration.verticalAlignment, baseScale);

            Unicode.Rune currentRune, previousRune = Unicode.BadRune;//input text unicode

            for (int k = 0, length = glyphOTFs.Length; k < length; k++)
            {
                var glyphOTF = glyphOTFs[k];

                var bytePosition = (int)glyphOTF.cluster;
                characters.GotoByteIndex(bytePosition);
                currentRune = characters.Current;
                if (bytePosition >= currentTextSpan.endIndex)
                {
                    currentTextSpan = textSpans[textSpanCounter++];
                    if (previousFontMaterialIndex != currentTextSpan.fontMaterialIndex)
                    {
                        var fontAssetRef = fontAssetRefs[currentTextSpan.fontMaterialIndex];
                        currentFontEntity = fontEntities[fontAssetRefs[currentTextSpan.fontMaterialIndex]];
                        currentFont = ref dynamicFontAssetsLookup[currentFontEntity].blob.Value;
                        scaledDynamicFont.Update(ref currentFont, out xNativeToUnity, out yNativeToUnity);
                        previousFontMaterialIndex = currentTextSpan.fontMaterialIndex;
                    }
                }

                if (lineCount == 0)
                    topAnchor = GetTopAnchorForConfig(ref scaledDynamicFont, baseConfiguration.verticalAlignment, baseScale, topAnchor);
                bottomAnchor = GetBottomAnchorForConfig(ref scaledDynamicFont, baseConfiguration.verticalAlignment, baseScale, bottomAnchor);

                #region Look up Character Data
                if (!currentFont.glyphs.TryGetValue(glyphOTF.codepoint, out var glyphBlob))
                {

                    Debug.Log($"glyph {(char)currentRune.value} not found in font entity {currentFontEntity}");
                    var bla = currentFont.glyphs.GetValueArray(Allocator.Temp);
                    foreach (var glyph in bla)
                    {
                        Debug.Log($"glyph {glyph.glyphID}");
                    }
                    continue;
                }

                // Cache glyph metrics
                var currentGlyphExtents = glyphBlob.glyphExtents;
                var x_bearing = currentGlyphExtents.x_bearing;
                var y_bearing = currentGlyphExtents.y_bearing;
                var glyphHeight = currentGlyphExtents.height;
                var glyphWidth = currentGlyphExtents.width;

                float adjustedScale = currentTextSpan.fontSize / currentFont.atlasSamplingPointSize * (baseConfiguration.isOrthographic ? 1 : 0.1f);
                float elementAscentLine = scaledDynamicFont.ascender;
                float elementDescentLine = scaledDynamicFont.descender;

                //synthesize superscript and subscript unless it is a digit. Most opentype fonts should
                //have dedicated glyphs for digits when enabling the 'subs' and 'sups' tags
                float fontScaleMultiplier = 1;
                float m_BaselineOffset = 0;
                if ((currentTextSpan.fontStyle & FontStyles.Subscript) == FontStyles.Subscript && !currentRune.IsDigit())
                {
                    fontScaleMultiplier = scaledDynamicFont.subScriptEmXSize;
                    m_BaselineOffset = -scaledDynamicFont.subScriptEmYOffset * adjustedScale;
                }
                else if ((currentTextSpan.fontStyle & FontStyles.Superscript) == FontStyles.Superscript && !currentRune.IsDigit())
                {
                    fontScaleMultiplier = scaledDynamicFont.superScriptEmXSize;
                    m_BaselineOffset = scaledDynamicFont.superScriptEmYOffset * adjustedScale;
                }

                currentElementScale = adjustedScale * fontScaleMultiplier;
                float baselineOffset = scaledDynamicFont.baseLine * adjustedScale * fontScaleMultiplier;
                #endregion

                // Optimization to avoid calling this more than once per character.
                bool isWhiteSpace = currentRune.value <= 0xFFFF && currentRune.IsWhiteSpace();

                // Handle Mono Spacing
                #region Handle Mono Spacing
                float monoAdvance = 0;
                if (currentTextSpan.monoSpacing != 0)
                {
                    monoAdvance =
                        (currentTextSpan.monoSpacing / 2 - (glyphWidth / 2 + x_bearing) * currentElementScale);  // * (1 - charWidthAdjDelta);
                    xAdvance += monoAdvance;
                }
                #endregion

                // Set Padding based on selected font style
                #region Handle Style Padding
                float boldSpacingAdjustment = 0;
                float style_padding = 0;
                //if bold is requested and current font is not bold (=it has not been found), then simulate bold
                bool simulateBold = (currentTextSpan.fontStyle & FontStyles.Bold) == FontStyles.Bold && currentFontAssetRef.weight != (int)FontWeight.Bold;
                if (simulateBold) 
                {
                    style_padding = 0;
                    boldSpacingAdjustment = currentFont.boldStyleSpacing;
                }
                #endregion Handle Style Padding

                // Determine the position of the vertices of the Character or Sprite.
                #region Calculate Vertices Position
                var renderGlyph = new RenderGlyph();


                // top left is used to position bottom left and top right
                float2 topLeft;
                var xOffset = glyphOTF.xOffset * xNativeToUnity;
                var yOffset = glyphOTF.yOffset * yNativeToUnity;
                topLeft.x = xAdvance + (x_bearing * currentTextSpan.fxScale - currentFont.materialPadding - style_padding + xOffset) * currentElementScale;
                topLeft.y = baselineOffset + (y_bearing + currentFont.materialPadding + yOffset) * currentElementScale + m_BaselineOffset;

                float2 bottomLeft;
                bottomLeft.x = topLeft.x;
                bottomLeft.y = topLeft.y - ((glyphHeight + currentFont.materialPadding * 2) * currentElementScale);

                float2 topRight;
                topRight.x = bottomLeft.x + (glyphWidth * currentTextSpan.fxScale + currentFont.materialPadding * 2 + style_padding * 2) * currentElementScale;
                topRight.y = topLeft.y;

                // Bottom right unused
                #endregion

                #region Setup UVA
                var glyphRect = glyphBlob.glyphRect;
                float2 blUVA, tlUVA, trUVA, brUVA;
                blUVA.x = (glyphRect.x - currentFont.materialPadding - style_padding) / currentFont.atlasWidth;
                blUVA.y = (glyphRect.y - currentFont.materialPadding - style_padding) / currentFont.atlasHeight;

                tlUVA.x = blUVA.x;
                tlUVA.y = (glyphRect.y + currentFont.materialPadding + style_padding + glyphRect.height) / currentFont.atlasHeight;

                trUVA.x = (glyphRect.x + currentFont.materialPadding + style_padding + glyphRect.width) / currentFont.atlasWidth;
                trUVA.y = tlUVA.y;

                brUVA.x = trUVA.x;
                brUVA.y = blUVA.y;

                renderGlyph.blUVA = blUVA;
                renderGlyph.trUVA = trUVA;
                #endregion

                #region Setup UVB
                //Setup UV2 based on Character Mapping Options Selected
                //m_horizontalMapping case TextureMappingOptions.Character
                float2 blUVC, tlUVC, trUVC, brUVC;
                blUVC.x = 0;
                tlUVC.x = 0;
                trUVC.x = 1;
                brUVC.x = 1;

                //m_verticalMapping case case TextureMappingOptions.Character
                blUVC.y = 0;
                tlUVC.y = 1;
                trUVC.y = 1;
                brUVC.y = 0;

                renderGlyph.blUVB = blUVC;
                renderGlyph.tlUVB = tlUVA;
                renderGlyph.trUVB = trUVC;
                renderGlyph.brUVB = brUVA;
                #endregion

                #region Setup Color
                renderGlyph.blColor = currentTextSpan.color;
                renderGlyph.tlColor = currentTextSpan.color;
                renderGlyph.trColor = currentTextSpan.color;
                renderGlyph.brColor = currentTextSpan.color;
                #endregion

                #region Pack Scale into renderGlyph.scale
                var scale = currentTextSpan.fontSize;
                if (simulateBold)
                    scale *= -1;

                renderGlyph.scale = scale;
                #endregion

                // Check if we need to Shear the rectangles for Italic styles
                #region Handle Italic & Shearing
                float bottomShear = 0f;
                //if italic is requested and current font is not italic (=it has not been found), then simulate italic
                bool simulateItalic = (currentTextSpan.fontStyle & FontStyles.Italic) == FontStyles.Italic && !currentFontAssetRef.isItalic;
                if (simulateItalic)
                {
                    // Shift Top vertices forward by half (Shear Value * height of character) and Bottom vertices back by same amount.
                    float shear_value = currentFont.italicsStyleSlant * 0.01f; 
                    float midPoint = ((scaledDynamicFont.capHeight - (scaledDynamicFont.baseLine + m_BaselineOffset)) / 2) * fontScaleMultiplier;
                    float topShear = shear_value * ((y_bearing + currentFont.materialPadding + style_padding - midPoint) * currentElementScale);
                    bottomShear = shear_value *
                                        ((y_bearing - glyphHeight - currentFont.materialPadding - style_padding - midPoint) *
                                         currentElementScale);

                    topLeft.x += topShear;
                    bottomLeft.x += bottomShear;
                    topRight.x += topShear;

                    renderGlyph.shear = topLeft.x - bottomLeft.x;
                }
                #endregion Handle Italics & Shearing

                // Handle Character FX Rotation
                #region Handle Character FX Rotation
                renderGlyph.rotationCCW = currentTextSpan.fxRotationAngleCCW;
                #endregion

                #region Store vertex information for the character or sprite.
                if (isLineStart)
                {
                    mappingWriter.AddLineStart(renderGlyphs.Length);
                    mappingWriter.AddWordStart(renderGlyphs.Length);
                }
                renderGlyph.trPosition = topRight;
                renderGlyph.blPosition = bottomLeft;
                if (Hint.Likely(currentRune.value != 10)) //do not render LF 
                {
                    renderGlyphs.Add(renderGlyph);
                    if (multiFont)
                    {
                        //var index = currentTextSpan.fontMaterialIndex == 0 ? 0 : currentTextSpan.fontMaterialIndex - 1;
                        m_selectorBuffer.Add(new FontMaterialSelectorForGlyph { fontMaterialIndex = (byte)currentTextSpan.fontMaterialIndex });
                    }

                    mappingWriter.AddCharNoTags(characterCount - 1, true);
                    mappingWriter.AddCharWithTags(k, true);
                    mappingWriter.AddBytes(characters.NextRuneByteIndex, currentRune.LengthInUtf8Bytes(), true);
                    //mappingWriter.AddBytes(characters.CurrentByteIndex, characters.CurrentByteIndex - bytePosition, true);
                }
                #endregion

                // Compute text metrics
                #region Compute Ascender & Descender values
                // Element Ascender in line space
                float elementAscender = elementAscentLine * currentElementScale + m_BaselineOffset;

                // Element Descender in line space
                float elementDescender = elementDescentLine * currentElementScale + m_BaselineOffset;


                float adjustedAscender = elementAscender;
                float adjustedDescender = elementDescender;

                // Max line ascender and descender in line space
                if (isLineStart || isWhiteSpace == false)
                {
                    // Special handling for Superscript and Subscript where we use the unadjusted line ascender and descender
                    if (m_BaselineOffset != 0)
                    {
                        adjustedAscender = math.max((elementAscender - m_BaselineOffset) / fontScaleMultiplier, adjustedAscender);
                        adjustedDescender = math.min((elementDescender - m_BaselineOffset) / fontScaleMultiplier, adjustedDescender);
                    }
                    maxLineAscender = math.max(adjustedAscender, maxLineAscender);
                    maxLineDescender = math.min(adjustedDescender, maxLineDescender);
                }
                #endregion

                #region XAdvance, Tabulation & Stops
                if (currentRune.value == 9)
                {
                    float tabSize = currentFont.tabWidth * currentFont.tabMultiple * currentElementScale;
                    float tabs = math.ceil(xAdvance / tabSize) * tabSize;
                    xAdvance = tabs > xAdvance ? tabs : xAdvance + tabSize;
                }
                else if (currentTextSpan.monoSpacing != 0)
                {
                    float monoAdjustment = currentTextSpan.monoSpacing - monoAdvance;
                    xAdvance += (monoAdjustment + ((currentFont.regularStyleSpacing) * currentEmScale) + currentTextSpan.cSpacing);
                    if (isWhiteSpace || currentRune.value == 0x200B)
                        xAdvance += baseConfiguration.wordSpacing * currentEmScale;
                }
                else
                {
                    xAdvance += (glyphOTF.xAdvance * xNativeToUnity * currentTextSpan.fxScale) * currentElementScale +
                                (currentFont.regularStyleSpacing + boldSpacingAdjustment) * currentEmScale + currentTextSpan.cSpacing;

                    if (isWhiteSpace || currentRune.value == 0x200B)
                        xAdvance += baseConfiguration.wordSpacing * currentEmScale;
                }
                #endregion XAdvance, Tabulation & Stops

                #region Check for Line Feed and Last Character
                if (isLineStart)
                    isLineStart = false;
                currentLineHeight = (scaledDynamicFont.ascender - scaledDynamicFont.descender) * baseScale; 
                ascentLineDelta = maxLineAscender - scaledDynamicFont.ascender * baseScale;
                decentLineDelta = scaledDynamicFont.descender * baseScale - maxLineDescender;
                //if (currentRune.value == 10 || currentRune.value == 11 || currentRune.value == 0x03 || currentRune.value == 0x2028 ||
                //    currentRune.value == 0x2029 || textConfiguration.m_characterCount == calliString.Length - 1)
                if (currentRune.value == 10)
                {
                    var glyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
                    var overrideMode = currentTextSpan.lineJustification;
                    if ((overrideMode) == HorizontalAlignmentOptions.Justified)
                    {
                        // Don't perform justified spacing for the last line in the paragraph.
                        overrideMode = HorizontalAlignmentOptions.Left;
                    }
                    ApplyHorizontalAlignmentToGlyphs(ref glyphsLine,
                                                     ref characterGlyphIndicesWithPreceedingSpacesInLine,
                                                     baseConfiguration.maxLineWidth,
                                                     overrideMode);
                    startOfLineGlyphIndex = renderGlyphs.Length;
                    if (lineCount > 0)
                    {
                        accumulatedVerticalOffset += currentLineHeight + ascentLineDelta;
                        if (lastCommittedStartOfLineGlyphIndex != startOfLineGlyphIndex)
                        {
                            ApplyVerticalOffsetToGlyphs(ref glyphsLine, accumulatedVerticalOffset);
                            lastCommittedStartOfLineGlyphIndex = startOfLineGlyphIndex;
                        }
                    }
                    accumulatedVerticalOffset += decentLineDelta;
                    //apply user configurable line and paragraph spacing
                    accumulatedVerticalOffset +=
                        (baseConfiguration.lineSpacing + (currentRune.value == 10 || currentRune.value == 0x2029 ? baseConfiguration.paragraphSpacing : 0)) * currentEmScale;

                    //reset line status
                    maxLineAscender = float.MinValue;
                    maxLineDescender = float.MaxValue;

                    lineCount++;
                    isLineStart = true;
                    bottomAnchor = GetBottomAnchorForConfig(ref scaledDynamicFont, baseConfiguration.verticalAlignment, baseScale);

                    xAdvance = 0;
                    previousRune = currentRune;
                    continue;
                }
                #endregion

                #region Word Wrapping
                // Handle word wrap
                if (baseConfiguration.maxLineWidth < float.MaxValue &&
                    baseConfiguration.maxLineWidth > 0 &&
                    xAdvance > baseConfiguration.maxLineWidth)
                {
                    bool dropSpace = false;

                    if (currentRune.value == 32 && previousRune.value != 32)
                    {
                        // What pushed us past the line width was a space character.
                        // The previous character was not a space, and we don't
                        // want to render this character at the start of the next line.
                        // We drop this space character instead and allow the next
                        // character to line-wrap, space or not.
                        dropSpace = true;
                        accumulatedSpaces--;
                    }

                    var yOffsetChange = 0f;  //font.lineHeight * currentElementScale;
                    var xOffsetChange = renderGlyphs[lastWordStartCharacterGlyphIndex].blPosition.x - bottomShear;
                    if (xOffsetChange > 0 && !dropSpace)  // Always allow one visible character
                    {
                        // Finish line based on alignment
                        var glyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex,
                                                                                  lastWordStartCharacterGlyphIndex - startOfLineGlyphIndex);
                        ApplyHorizontalAlignmentToGlyphs(ref glyphsLine,
                                                         ref characterGlyphIndicesWithPreceedingSpacesInLine,
                                                         baseConfiguration.maxLineWidth,
                                                         currentTextSpan.lineJustification);

                        if (lineCount > 0)
                        {
                            accumulatedVerticalOffset += currentLineHeight + ascentLineDelta;
                            ApplyVerticalOffsetToGlyphs(ref glyphsLine, accumulatedVerticalOffset);
                            lastCommittedStartOfLineGlyphIndex = startOfLineGlyphIndex;
                        }
                        accumulatedVerticalOffset += decentLineDelta;  // Todo: Delta should be computed per glyph
                        //apply user configurable line and paragraph spacing
                        accumulatedVerticalOffset += baseConfiguration.lineSpacing * currentEmScale;

                        //reset line status
                        maxLineAscender = float.MinValue;
                        maxLineDescender = float.MaxValue;

                        startOfLineGlyphIndex = lastWordStartCharacterGlyphIndex;
                        isLineStart = true;
                        lineCount++;

                        xAdvance -= xOffsetChange;

                        // Adjust the vertices of the previous render glyphs in the word
                        var glyphPtr = (RenderGlyph*)renderGlyphs.GetUnsafePtr();
                        for (int i = lastWordStartCharacterGlyphIndex; i < renderGlyphs.Length; i++)
                        {
                            glyphPtr[i].blPosition.y -= yOffsetChange;
                            glyphPtr[i].blPosition.x -= xOffsetChange;
                            glyphPtr[i].trPosition.y -= yOffsetChange;
                            glyphPtr[i].trPosition.x -= xOffsetChange;
                        }
                    }
                }
                //Detect start of word
                if (currentRune.value == 32 ||  //Space
                    currentRune.value == 9 ||  //Tab
                    currentRune.value == 45 ||  //Hyphen Minus
                    currentRune.value == 173 ||  //Soft hyphen
                    currentRune.value == 8203 ||  //Zero width space
                    currentRune.value == 8204 ||  //Zero width non-joiner
                    currentRune.value == 8205)  //Zero width joiner
                {
                    lastWordStartCharacterGlyphIndex = renderGlyphs.Length;
                    mappingWriter.AddWordStart(renderGlyphs.Length);
                }

                if (glyphOTF.codepoint == 1)
                {
                    accumulatedSpaces++;
                }
                #endregion
                previousRune = currentRune;
            }

            var finalGlyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
            {
                var overrideMode = currentTextSpan.lineJustification;
                if (overrideMode == HorizontalAlignmentOptions.Justified)
                {
                    // Don't perform justified spacing for the last line.
                    overrideMode = HorizontalAlignmentOptions.Left;
                }
                ApplyHorizontalAlignmentToGlyphs(ref finalGlyphsLine, ref characterGlyphIndicesWithPreceedingSpacesInLine, baseConfiguration.maxLineWidth, overrideMode);
                if (lineCount > 0)
                {
                    accumulatedVerticalOffset += currentLineHeight;
                    ApplyVerticalOffsetToGlyphs(ref finalGlyphsLine, accumulatedVerticalOffset);
                }
            }
            lineCount++;
            ApplyVerticalAlignmentToGlyphs(ref renderGlyphs, topAnchor, bottomAnchor, accumulatedVerticalOffset, baseConfiguration.verticalAlignment);
        }

        static float GetTopAnchorForConfig(ref ScaledDynamicFont  scaledDynamicFont, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.TopBase: return 0f;
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.TopAscent: return baseScale * math.max(scaledDynamicFont.ascender - scaledDynamicFont.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopDescent: return baseScale * math.min(scaledDynamicFont.descender - scaledDynamicFont.baseLine, oldValue);
                case VerticalAlignmentOptions.TopCap: return baseScale * math.max(scaledDynamicFont.capHeight - scaledDynamicFont.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopMean: return baseScale * math.max(scaledDynamicFont.xHeight - scaledDynamicFont.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static float GetBottomAnchorForConfig(ref ScaledDynamicFont scaledDynamicFont, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.BottomBase: return 0f;
                case VerticalAlignmentOptions.BottomAscent: return baseScale * math.max(scaledDynamicFont.ascender - scaledDynamicFont.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.BottomDescent: return baseScale * math.min(scaledDynamicFont.descender - scaledDynamicFont.baseLine, oldValue);
                case VerticalAlignmentOptions.BottomCap: return baseScale * math.max(scaledDynamicFont.capHeight - scaledDynamicFont.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.BottomMean: return baseScale * math.max(scaledDynamicFont.xHeight - scaledDynamicFont.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static unsafe void ApplyHorizontalAlignmentToGlyphs(ref NativeArray<RenderGlyph> glyphs,
                                                            ref FixedList512Bytes<int> characterGlyphIndicesWithPreceedingSpacesInLine,
                                                            float width,
                                                            HorizontalAlignmentOptions alignMode)
        {
            if ((alignMode) == HorizontalAlignmentOptions.Left)
            {
                characterGlyphIndicesWithPreceedingSpacesInLine.Clear();
                return;
            }

            var glyphsPtr = (RenderGlyph*)glyphs.GetUnsafePtr();
            if ((alignMode) == HorizontalAlignmentOptions.Center)
            {
                float offset = glyphsPtr[glyphs.Length - 1].trPosition.x / 2f;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    glyphsPtr[i].blPosition.x -= offset;
                    glyphsPtr[i].trPosition.x -= offset;
                }
            }
            else if ((alignMode) == HorizontalAlignmentOptions.Right)
            {
                float offset = glyphsPtr[glyphs.Length - 1].trPosition.x;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    glyphsPtr[i].blPosition.x -= offset;
                    glyphsPtr[i].trPosition.x -= offset;
                }
            }
            else  // Justified
            {
                float nudgePerSpace = (width - glyphsPtr[glyphs.Length - 1].trPosition.x) / characterGlyphIndicesWithPreceedingSpacesInLine.Length;
                float accumulatedOffset = 0f;
                int indexInIndices = 0;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    while (indexInIndices < characterGlyphIndicesWithPreceedingSpacesInLine.Length &&
                           characterGlyphIndicesWithPreceedingSpacesInLine[indexInIndices] == i)
                    {
                        accumulatedOffset += nudgePerSpace;
                        indexInIndices++;
                    }

                    glyphsPtr[i].blPosition.x += accumulatedOffset;
                    glyphsPtr[i].trPosition.x += accumulatedOffset;
                }
            }
            characterGlyphIndicesWithPreceedingSpacesInLine.Clear();
        }

        static unsafe void ApplyVerticalOffsetToGlyphs(ref NativeArray<RenderGlyph> glyphs, float accumulatedVerticalOffset)
        {
            for (int i = 0; i < glyphs.Length; i++)
            {
                var glyph = glyphs[i];
                glyph.blPosition.y -= accumulatedVerticalOffset;
                glyph.trPosition.y -= accumulatedVerticalOffset;
                glyphs[i] = glyph;
            }
        }

        static unsafe void ApplyVerticalAlignmentToGlyphs(ref DynamicBuffer<RenderGlyph> glyphs,
                                                          float topAnchor,
                                                          float bottomAnchor,
                                                          float accumulatedVerticalOffset,
                                                          VerticalAlignmentOptions alignMode)
        {
            var glyphsPtr = (RenderGlyph*)glyphs.GetUnsafePtr();
            switch (alignMode)
            {
                case VerticalAlignmentOptions.TopBase:
                    return;
                case VerticalAlignmentOptions.TopAscent:
                case VerticalAlignmentOptions.TopDescent:
                case VerticalAlignmentOptions.TopCap:
                case VerticalAlignmentOptions.TopMean:
                    {
                        // Positions were calculated relative to the baseline.
                        // Shift everything down so that y = 0 is on the target line.
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            glyphsPtr[i].blPosition.y -= topAnchor;
                            glyphsPtr[i].trPosition.y -= topAnchor;
                        }
                        break;
                    }
                case VerticalAlignmentOptions.BottomBase:
                case VerticalAlignmentOptions.BottomAscent:
                case VerticalAlignmentOptions.BottomDescent:
                case VerticalAlignmentOptions.BottomCap:
                case VerticalAlignmentOptions.BottomMean:
                    {
                        float offset = accumulatedVerticalOffset - bottomAnchor;
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            glyphsPtr[i].blPosition.y += offset;
                            glyphsPtr[i].trPosition.y += offset;
                        }
                        break;
                    }
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                    {
                        float fullHeight = accumulatedVerticalOffset - bottomAnchor + topAnchor;
                        float offset = fullHeight / 2f;
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            glyphsPtr[i].blPosition.y += offset;
                            glyphsPtr[i].trPosition.y += offset;
                        }
                        break;
                    }
            }
        }
    }
}