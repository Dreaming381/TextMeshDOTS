using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.TextCore.Text;
using HarfBuzz;
using Font = HarfBuzz.Font;
using UnityEngine;
using Unity.Profiling;



namespace TextMeshDOTS
{
    internal static class GlyphGeneration
    {
        /// <summary> This function logic follows TMPro_Private.GenerateTextMesh() </summary>
        internal static unsafe void CreateRenderGlyphs(ref NativeFont nativeFont, //to-do: pass array of fonts, select right one
                                                       ref FontMaterialSet fontMaterialSet,
                                                       ref DynamicBuffer<RenderGlyph> renderGlyphs,
                                                       ref GlyphMappingWriter mappingWriter,                                                       
                                                       in DynamicBuffer<CalliByte> calliBytes,
                                                       in DynamicBuffer<GlyphOTF> glyphOTFs,
                                                       in DynamicBuffer<TextSpan> textSpans,
                                                       in TextBaseConfiguration baseConfiguration)
        {
            renderGlyphs.Clear();

            // Initialize textConfiguration which stores all fields that are modified by RichText Tags
            int textSpanCounter = 0;
            var currentTextSpan = textSpans[textSpanCounter++];
            var calliString = new CalliString(calliBytes);

            var characters = calliString.GetEnumerator();

            ref FontBlob font = ref fontMaterialSet[currentTextSpan.fontMaterialIndex];

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

            #region AtlasFactor
            // Unity scales native Opentype metrics in GlyphRect and GlyphMetrics by 
            // (unit * atlasSamplingPointSize / Opentype.xScale)
            // So raw values from Opentype font need to be scaled like that as well.

            var xNativeToUnity = font.atlasSamplingPointSize / nativeFont.yScale;
            var yNativeToUnity = font.atlasSamplingPointSize / nativeFont.xScale;

            var fontBaseLine = 0f; //fontBaseLine
            #endregion

            float subScriptFactor = nativeFont.subScriptEmXSize / nativeFont.xScale;
            float superScriptFactor = nativeFont.superScriptEmXSize / nativeFont.xScale;
            float subScriptOffset = nativeFont.subScriptEmYOffset * yNativeToUnity;
            float superScriptOffset = nativeFont.superScriptEmYOffset * yNativeToUnity;

            //Debug.Log($" horizontalAscender {horizontalAscender} b {b} {{xScale / baseConfiguration.fontSize {xScale / baseConfiguration.fontSize} xScale * baseScaleFactor {xScale * baseScaleFactor} baseConfiguration.fontSize {baseConfiguration.fontSize}");
            var fontExtends = nativeFont.GetFontExtents(Direction.LeftToRight);
            float ascentLine = fontExtends.ascender * xNativeToUnity;
            float descentLine = fontExtends.descender * xNativeToUnity;
            float meanLine = nativeFont.xHeight * xNativeToUnity;
            float capLine = nativeFont.capHeight * xNativeToUnity;

            // Calculate the scale of the font based on selected font size and sampling point size.
            // baseScale is calculated using the font asset assigned to the text object.            
            float baseScale = baseConfiguration.fontSize / font.atlasSamplingPointSize * (baseConfiguration.isOrthographic ? 1 : 0.1f);
            float currentElementScale = baseScale;
            float currentEmScale = baseConfiguration.fontSize * 0.01f * (baseConfiguration.isOrthographic ? 1 : 0.1f);

            float topAnchor = GetTopAnchorForConfig(fontBaseLine, ascentLine, descentLine, capLine, meanLine, baseConfiguration.verticalAlignment, baseScale);
            float bottomAnchor = GetBottomAnchorForConfig(fontBaseLine, ascentLine, descentLine, capLine, meanLine, baseConfiguration.verticalAlignment, baseScale);

            Unicode.Rune currentRune, previousRune = Unicode.BadRune;//input text unicode

            for (int k = 0, length = glyphOTFs.Length; k < length; k++)
            {
                var glyphOTF = glyphOTFs[k];

                var bytePosition = (int)glyphOTF.cluster;
                characters.GotoIndex(bytePosition);
                currentRune = characters.Current;
                if (bytePosition >= currentTextSpan.endIndex)
                    currentTextSpan = textSpans[textSpanCounter++];

                font = ref fontMaterialSet[currentTextSpan.fontMaterialIndex];
                if (lineCount == 0)
                    topAnchor = GetTopAnchorForConfig(fontBaseLine, ascentLine, descentLine, capLine, meanLine, baseConfiguration.verticalAlignment, baseScale, topAnchor);
                bottomAnchor = GetBottomAnchorForConfig(fontBaseLine, ascentLine, descentLine, capLine, meanLine, baseConfiguration.verticalAlignment, baseScale, bottomAnchor);

                #region Look up Character Data
                if (!font.glyphs.TryGetValue((int)glyphOTF.codepoint, out var glyphBlob))
                    continue;

                // Cache glyph metrics
                var currentGlyphMetrics = glyphBlob.glyphMetrics;
                var x_bearing = currentGlyphMetrics.horizontalBearingX;
                var y_bearing = currentGlyphMetrics.horizontalBearingY;
                var glyphHeight = currentGlyphMetrics.height;
                var glyphWidth = currentGlyphMetrics.width;
                var glyphScale = glyphBlob.glyphScale;

                float adjustedScale = currentTextSpan.fontSize / font.atlasSamplingPointSize * (baseConfiguration.isOrthographic ? 1 : 0.1f);
                float elementAscentLine = ascentLine;
                float elementDescentLine = descentLine;

                //synthesize superscript and subscript unless it is a digit. Most opentype fonts should
                //have dedicated glyphs for digits when enabling the 'subs' and 'sups' tags
                float fontScaleMultiplier = 1;
                float m_BaselineOffset = 0;
                if ((currentTextSpan.fontStyle & FontStyles.Subscript) == FontStyles.Subscript && !currentRune.IsDigit())
                {
                    fontScaleMultiplier = subScriptFactor;
                    m_BaselineOffset = -subScriptOffset * adjustedScale;
                }
                else if ((currentTextSpan.fontStyle & FontStyles.Superscript) == FontStyles.Superscript && !currentRune.IsDigit())
                {
                    fontScaleMultiplier = superScriptFactor;
                    m_BaselineOffset = superScriptOffset * adjustedScale;
                }

                currentElementScale = adjustedScale * fontScaleMultiplier * glyphScale;
                float baselineOffset = fontBaseLine * adjustedScale * fontScaleMultiplier;
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
                if ((currentTextSpan.fontStyle & FontStyles.Bold) == FontStyles.Bold)
                {
                    style_padding = 0;
                    boldSpacingAdjustment = font.boldStyleSpacing;
                }
                #endregion Handle Style Padding

                // Determine the position of the vertices of the Character or Sprite.
                #region Calculate Vertices Position
                var renderGlyph = new RenderGlyph();


                // top left is used to position bottom left and top right
                float2 topLeft;
                var xOffset = glyphOTF.xOffset * xNativeToUnity;
                var yOffset = glyphOTF.yOffset * yNativeToUnity;
                topLeft.x = xAdvance + (x_bearing * currentTextSpan.fxScale - font.materialPadding - style_padding + xOffset) * currentElementScale;
                topLeft.y = baselineOffset + (y_bearing + font.materialPadding + yOffset) * currentElementScale + m_BaselineOffset;

                float2 bottomLeft;
                bottomLeft.x = topLeft.x;
                bottomLeft.y = topLeft.y - ((glyphHeight + font.materialPadding * 2) * currentElementScale);

                float2 topRight;
                topRight.x = bottomLeft.x + (glyphWidth * currentTextSpan.fxScale + font.materialPadding * 2 + style_padding * 2) * currentElementScale;
                topRight.y = topLeft.y;

                // Bottom right unused
                #endregion

                #region Setup UVA
                var glyphRect = glyphBlob.glyphRect;
                float2 blUVA, tlUVA, trUVA, brUVA;
                blUVA.x = (glyphRect.x - font.materialPadding - style_padding) / font.atlasWidth;
                blUVA.y = (glyphRect.y - font.materialPadding - style_padding) / font.atlasHeight;

                tlUVA.x = blUVA.x;
                tlUVA.y = (glyphRect.y + font.materialPadding + style_padding + glyphRect.height) / font.atlasHeight;

                trUVA.x = (glyphRect.x + font.materialPadding + style_padding + glyphRect.width) / font.atlasWidth;
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
                if ((currentTextSpan.fontStyle & FontStyles.Bold) == FontStyles.Bold)
                    scale *= -1;

                renderGlyph.scale = scale;
                #endregion

                // Check if we need to Shear the rectangles for Italic styles
                #region Handle Italic & Shearing
                float bottomShear = 0f;
                if ((currentTextSpan.fontStyle & FontStyles.Italic) == FontStyles.Italic)
                {
                    // Shift Top vertices forward by half (Shear Value * height of character) and Bottom vertices back by same amount.
                    float shear_value = currentTextSpan.italicAngle * 0.01f;
                    float midPoint = ((capLine - (fontBaseLine + m_BaselineOffset)) / 2) * fontScaleMultiplier;
                    float topShear = shear_value * ((y_bearing + font.materialPadding + style_padding - midPoint) * currentElementScale);
                    bottomShear = shear_value *
                                        ((y_bearing - glyphHeight - font.materialPadding - style_padding - midPoint) *
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
                renderGlyphs.Add(renderGlyph);
                fontMaterialSet.WriteFontMaterialIndexForGlyph(currentTextSpan.fontMaterialIndex);
                mappingWriter.AddCharNoTags(characterCount - 1, true);
                mappingWriter.AddCharWithTags(k, true);
                mappingWriter.AddBytes(characters.CurrentByteIndex, currentRune.LengthInUtf8Bytes(), true);
                //mappingWriter.AddBytes(characters.CurrentByteIndex, characters.CurrentByteIndex - bytePosition, true);
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
                    float tabSize = font.tabWidth * font.tabMultiple * currentElementScale;
                    float tabs = math.ceil(xAdvance / tabSize) * tabSize;
                    xAdvance = tabs > xAdvance ? tabs : xAdvance + tabSize;
                }
                else if (currentTextSpan.monoSpacing != 0)
                {
                    float monoAdjustment = currentTextSpan.monoSpacing - monoAdvance;
                    xAdvance += (monoAdjustment + ((font.regularStyleSpacing) * currentEmScale) + currentTextSpan.cSpacing);
                    if (isWhiteSpace || currentRune.value == 0x200B)
                        xAdvance += baseConfiguration.wordSpacing * currentEmScale;
                }
                else
                {
                    xAdvance += (glyphOTF.xAdvance * xNativeToUnity * currentTextSpan.fxScale) * currentElementScale +
                                (font.regularStyleSpacing + boldSpacingAdjustment) * currentEmScale + currentTextSpan.cSpacing;

                    if (isWhiteSpace || currentRune.value == 0x200B)
                        xAdvance += baseConfiguration.wordSpacing * currentEmScale;
                }
                #endregion XAdvance, Tabulation & Stops

                #region Check for Line Feed and Last Character
                if (isLineStart)
                    isLineStart = false;
                currentLineHeight = (ascentLine - descentLine) * baseScale; //font.lineHeight * baseScale;  //why not (font.ascentLine-font.baseLine) * baseScale ?
                ascentLineDelta = maxLineAscender - ascentLine * baseScale;
                decentLineDelta = descentLine * baseScale - maxLineDescender;
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
                    bottomAnchor = GetBottomAnchorForConfig(fontBaseLine, ascentLine, descentLine, capLine, meanLine, baseConfiguration.verticalAlignment, baseScale);

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
                        accumulatedVerticalOffset +=
                            (baseConfiguration.lineSpacing);

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

        static float GetTopAnchorForConfig(float fontBaseLine, float ascentLine, float descentLine, float capLine, float meanLine, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.TopBase: return 0f;
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.TopAscent: return baseScale * math.max(ascentLine - fontBaseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopDescent: return baseScale * math.min(descentLine - fontBaseLine, oldValue);
                case VerticalAlignmentOptions.TopCap: return baseScale * math.max(capLine - fontBaseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopMean: return baseScale * math.max(meanLine - fontBaseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static float GetBottomAnchorForConfig(float fontBaseLine, float ascentLine, float descentLine, float capLine, float meanLine, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.BottomBase: return 0f;
                case VerticalAlignmentOptions.BottomAscent: return baseScale * math.max(ascentLine - fontBaseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.BottomDescent: return baseScale * math.min(descentLine - fontBaseLine, oldValue);
                case VerticalAlignmentOptions.BottomCap: return baseScale * math.max(capLine - fontBaseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.BottomMean: return baseScale * math.max(meanLine - fontBaseLine, math.select(oldValue, float.NegativeInfinity, replace));
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