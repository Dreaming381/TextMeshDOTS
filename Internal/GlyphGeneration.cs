using TextMeshDOTS.Rendering;
using TextMeshDOTS.RichText;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TextCore.Text;
using HarfBuzz;
using Font = HarfBuzz.Font;
using Buffer = HarfBuzz.Buffer;
using System;

namespace TextMeshDOTS
{
    internal static class GlyphGeneration
    {
        /// <summary> This function logic follows TMPro_Private.GenerateTextMesh() </summary>
        internal static unsafe void CreateRenderGlyphs(Font HBfont,
                                                       ref DynamicBuffer<RenderGlyph> renderGlyphs,
                                                       ref GlyphMappingWriter mappingWriter,
                                                       ref FontMaterialSet fontMaterialSet,
                                                       ref TextConfigurationStack textConfigurationStack,
                                                       in DynamicBuffer<CalliByte> calliBytes,
                                                       in TextBaseConfiguration baseConfiguration)
        {
            renderGlyphs.Clear();

            // Initialize textConfiguration which stores all fields that are modified by RichText Tags
            textConfigurationStack.Reset(baseConfiguration);
            var calliString = new CalliString(calliBytes);
            
            var buffer = new Buffer(Direction.LeftToRight, Script.Latin);
            //var language = HB.hb_language_from_string("en", -1);
            //buffer.Language = language;

            buffer.AddText(calliBytes.Reinterpret<byte>());

            HB.hb_shape(HBfont.ptr, buffer.ptr, IntPtr.Zero, 0);
            var glyphInfos = buffer.GlyphInfo();
            var glyphPositions = buffer.GlyphPositions();
            var characters = calliString.GetEnumerator();

            var textConfiguration = new ActiveTextConfiguration(ref textConfigurationStack);
            ref FontBlob font = ref fontMaterialSet[textConfiguration.m_currentFontMaterialIndex];

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

            //scale factor to sync Harfbuzz with FontAsset and FontAtlas
            var baseScaleFactor = baseConfiguration.fontSize * (baseConfiguration.isOrthographic ? 1 : 0.1f);
            HBfont.GetScale(out int x_scale, out int y_scale);
            float xScale = (float)x_scale;
            float baseScaleHarfbuzz = xScale * baseScaleFactor;

            // Calculate the scale of the font based on selected font size and sampling point size.
            // baseScale is calculated using the font asset assigned to the text object.            
            float baseScale = font.scale / font.pointSize * baseScaleFactor;

            float currentElementScale = baseScale;
            float currentEmScale = 0.01f * baseScaleFactor;

            float topAnchor = GetTopAnchorForConfig(ref fontMaterialSet[0], baseConfiguration.verticalAlignment, baseScale);
            float bottomAnchor = GetBottomAnchorForConfig(ref fontMaterialSet[0], baseConfiguration.verticalAlignment, baseScale);

            TextGenerationStateCommands textGenerationStateCommands = default;
            textGenerationStateCommands.Reset();
            Unicode.Rune currentRune, previousRune = Unicode.BadRune;//input text unicode
            for (int k = 0, length = glyphInfos.Length; k < length; k++)
            {                
                var glyphInfo = glyphInfos[k];
                var glyphPosition = glyphPositions[k];
                var bytePosition = (int)glyphInfo.cluster;
                characters.GotoIndex(bytePosition);
                currentRune = characters.Current;

                font = ref fontMaterialSet[textConfiguration.m_currentFontMaterialIndex];
                textConfigurationStack.m_isParsingText = false;
                if (lineCount == 0)
                    topAnchor = GetTopAnchorForConfig(ref font, baseConfiguration.verticalAlignment, baseScale, topAnchor);
                bottomAnchor = GetBottomAnchorForConfig(ref font, baseConfiguration.verticalAlignment, baseScale, bottomAnchor);

                // Handle Font Styles like LowerCase, UpperCase and SmallCaps.
                SwapRune(ref currentRune, ref textConfiguration, out float smallCapsMultiplier);

                // Look up Character Data. TMP uses a backing array,
                // we pull character directly from FontBlob and continue when not found
                #region Look up Character Data
                if (!font.glyphs.TryGetValue((int)glyphInfo.codepoint, out var glyphBlob))
                    continue;


                //Debug.Log($"charPosition {charPosition} unicode {unicode}({(char)unicode}) glyph {glyphInfo.codepoint}");

                float adjustedScale = font.scale / font.pointSize * textConfiguration.m_currentFontSize * (baseConfiguration.isOrthographic ? 1 : 0.1f);
                float elementAscentLine = font.ascentLine;
                float elementDescentLine = font.descentLine;

                currentElementScale = adjustedScale * textConfiguration.m_fontScaleMultiplier * glyphBlob.glyphScale;  //* m_cached_TextElement.m_Scale
                float baselineOffset = font.baseLine * adjustedScale * textConfiguration.m_fontScaleMultiplier * font.scale;
                #endregion

                // Cache glyph metrics
                var currentGlyphMetrics = glyphBlob.glyphMetrics;

                // Optimization to avoid calling this more than once per character.
                bool isWhiteSpace = currentRune.value <= 0xFFFF && currentRune.IsWhiteSpace();

                // Handle Mono Spacing
                #region Handle Mono Spacing
                float monoAdvance = 0;
                if (textConfiguration.m_monoSpacing != 0)
                {
                    monoAdvance =
                        (textConfiguration.m_monoSpacing / 2 - (currentGlyphMetrics.width / 2 + currentGlyphMetrics.horizontalBearingX) * currentElementScale);  // * (1 - charWidthAdjDelta);
                    xAdvance += monoAdvance;
                }
                #endregion

                // Set Padding based on selected font style
                #region Handle Style Padding
                float boldSpacingAdjustment = 0;
                float style_padding = 0;
                if ((textConfiguration.m_fontStyleInternal & FontStyles.Bold) == FontStyles.Bold)
                {
                    style_padding = 0;
                    boldSpacingAdjustment = font.boldStyleSpacing;
                }
                #endregion Handle Style Padding

                // Determine the position of the vertices of the Character or Sprite.
                #region Calculate Vertices Position
                var renderGlyph = new RenderGlyph { unicode = glyphBlob.glyphIndex };

                // top left is used to position bottom left and top right
                float2 topLeft;
                var xOffset = glyphPosition.xOffset / xScale * baseScaleFactor;
                var yOffset = glyphPosition.yOffset / xScale * baseScaleFactor;
                topLeft.x = xAdvance + xOffset+
                            ((currentGlyphMetrics.horizontalBearingX * textConfiguration.m_fxScale.x - font.materialPadding - style_padding) * currentElementScale);  // * (1 - m_charWidthAdjDelta));
                topLeft.y = baselineOffset + yOffset +
                            (currentGlyphMetrics.horizontalBearingY + font.materialPadding) * currentElementScale - textConfiguration.m_lineOffset + textConfiguration.m_baselineOffset;

                float2 bottomLeft;
                bottomLeft.x = topLeft.x;
                bottomLeft.y = topLeft.y - ((currentGlyphMetrics.height + font.materialPadding * 2) * currentElementScale);

                float2 topRight;
                topRight.x = bottomLeft.x + (currentGlyphMetrics.width * textConfiguration.m_fxScale.x + font.materialPadding * 2 + style_padding * 2) * currentElementScale;
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
                renderGlyph.blColor = textConfiguration.m_htmlColor;
                renderGlyph.tlColor = textConfiguration.m_htmlColor;
                renderGlyph.trColor = textConfiguration.m_htmlColor;
                renderGlyph.brColor = textConfiguration.m_htmlColor;
                #endregion

                #region Pack Scale into renderGlyph.scale
                var scale = textConfiguration.m_currentFontSize;  // * math.abs(lossyScale) * (1 - m_charWidthAdjDelta);
                if ((textConfiguration.m_fontStyleInternal & FontStyles.Bold) == FontStyles.Bold)
                    scale *= -1;

                renderGlyph.scale = scale;
                #endregion

                // Check if we need to Shear the rectangles for Italic styles
                #region Handle Italic & Shearing
                float bottomShear = 0f;
                if ((textConfiguration.m_fontStyleInternal & FontStyles.Italic) == FontStyles.Italic)
                {
                    // Shift Top vertices forward by half (Shear Value * height of character) and Bottom vertices back by same amount.
                    float shear_value = textConfiguration.m_italicAngle * 0.01f;
                    float midPoint = ((font.capLine - (font.baseLine + textConfiguration.m_baselineOffset)) / 2) * textConfiguration.m_fontScaleMultiplier * font.scale;
                    float topShear = shear_value * ((currentGlyphMetrics.horizontalBearingY + font.materialPadding + style_padding - midPoint) * currentElementScale);
                    bottomShear = shear_value *
                                        ((currentGlyphMetrics.horizontalBearingY - currentGlyphMetrics.height - font.materialPadding - style_padding - midPoint) *
                                         currentElementScale);

                    topLeft.x += topShear;
                    bottomLeft.x += bottomShear;
                    topRight.x += topShear;

                    renderGlyph.shear = topLeft.x - bottomLeft.x;
                }
                #endregion Handle Italics & Shearing

                // Handle Character FX Rotation
                #region Handle Character FX Rotation
                renderGlyph.rotationCCW = textConfiguration.m_fxRotationAngleCCW;
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
                fontMaterialSet.WriteFontMaterialIndexForGlyph(textConfiguration.m_currentFontMaterialIndex);
                mappingWriter.AddCharNoTags(characterCount - 1, true);
                mappingWriter.AddCharWithTags(k, true);
                //mappingWriter.AddBytes(prevCurNext.current.CurrentByteIndex, currentRune.LengthInUtf8Bytes(), true);
                #endregion

                // Compute text metrics
                #region Compute Ascender & Descender values
                // Element Ascender in line space
                float elementAscender = elementAscentLine * currentElementScale / smallCapsMultiplier + textConfiguration.m_baselineOffset;

                // Element Descender in line space
                float elementDescender = elementDescentLine * currentElementScale / smallCapsMultiplier + textConfiguration.m_baselineOffset;

                float adjustedAscender = elementAscender;
                float adjustedDescender = elementDescender;

                // Max line ascender and descender in line space
                if (isLineStart || isWhiteSpace == false)
                {
                    // Special handling for Superscript and Subscript where we use the unadjusted line ascender and descender
                    if (textConfiguration.m_baselineOffset != 0)
                    {
                        adjustedAscender = math.max((elementAscender - textConfiguration.m_baselineOffset) / textConfiguration.m_fontScaleMultiplier, adjustedAscender);
                        adjustedDescender = math.min((elementDescender - textConfiguration.m_baselineOffset) / textConfiguration.m_fontScaleMultiplier, adjustedDescender);
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
                else if (textConfiguration.m_monoSpacing != 0)
                {
                    float monoAdjustment = textConfiguration.m_monoSpacing - monoAdvance;
                    xAdvance += (monoAdjustment + ((font.regularStyleSpacing) * currentEmScale) + textConfiguration.m_cSpacing);  // * (1 - m_charWidthAdjDelta);
                    if (isWhiteSpace || currentRune.value == 0x200B)
                        xAdvance += baseConfiguration.wordSpacing * currentEmScale;
                }
                else
                {
                    //xAdvance +=
                    //    ((currentGlyphMetrics.horizontalAdvance * textConfiguration.m_fxScale.x) * currentElementScale +
                    //     (font.regularStyleSpacing + boldSpacingAdjustment) * currentEmScale + textConfiguration.m_cSpacing);  // * (1 - m_charWidthAdjDelta);

                    xAdvance += glyphPosition.xAdvance / xScale * baseScaleFactor + 
                                (font.regularStyleSpacing + boldSpacingAdjustment) * currentEmScale + textConfiguration.m_cSpacing;  // * (1 - m_charWidthAdjDelta);;

                    if (isWhiteSpace || currentRune.value == 0x200B)
                        xAdvance += baseConfiguration.wordSpacing * currentEmScale;
                }
                #endregion XAdvance, Tabulation & Stops

                #region Check for Line Feed and Last Character
                if (isLineStart)
                    isLineStart = false;
                currentLineHeight = font.lineHeight * baseScale;  //why not (font.ascentLine-font.baseLine) * baseScale ?
                ascentLineDelta = maxLineAscender - font.ascentLine * baseScale;
                decentLineDelta = font.descentLine * baseScale - maxLineDescender;
                //if (currentRune.value == 10 || currentRune.value == 11 || currentRune.value == 0x03 || currentRune.value == 0x2028 ||
                //    currentRune.value == 0x2029 || textConfiguration.m_characterCount == calliString.Length - 1)
                if (currentRune.value == 10)
                {
                    var glyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
                    var overrideMode = textConfiguration.m_lineJustification;
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
                    bottomAnchor = GetBottomAnchorForConfig(ref font, baseConfiguration.verticalAlignment, baseScale);

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
                                                         textConfiguration.m_lineJustification);

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

                if (glyphInfo.codepoint == 1)
                {
                    accumulatedSpaces++;
                }
                #endregion
                previousRune = currentRune;
            }

            var finalGlyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
            {
                var overrideMode = textConfiguration.m_lineJustification;
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
            buffer.Dispose();
        }

        static float GetTopAnchorForConfig(ref FontBlob font, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.TopBase: return 0f;
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.TopAscent: return baseScale * math.max(font.ascentLine - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopDescent: return baseScale * math.min(font.descentLine - font.baseLine, oldValue);
                case VerticalAlignmentOptions.TopCap: return baseScale * math.max(font.capLine - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopMean: return baseScale * math.max(font.meanLine - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static float GetBottomAnchorForConfig(ref FontBlob font, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.BottomBase: return 0f;
                case VerticalAlignmentOptions.BottomAscent: return baseScale * math.max(font.ascentLine - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.BottomDescent: return baseScale * math.min(font.descentLine - font.baseLine, oldValue);
                case VerticalAlignmentOptions.BottomCap: return baseScale * math.max(font.capLine - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.BottomMean: return baseScale * math.max(font.meanLine - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
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

        static unsafe void SwapRune(ref Unicode.Rune rune, ref ActiveTextConfiguration textConfiguration, out float smallCapsMultiplier)
        {
            smallCapsMultiplier = 1f;

            // Todo: Burst does not support language methods, and char only supports the UTF-16 subset
            // of characters. We should encode upper and lower cross-references into the font blobs or
            // figure out the formulas for all other languages. Right now only ascii is supported.
            if ((textConfiguration.m_fontStyleInternal & FontStyles.UpperCase) == FontStyles.UpperCase)
            {
                // If this character is lowercase, switch to uppercase.
                rune = rune.ToUpper();
            }
            else if ((textConfiguration.m_fontStyleInternal & FontStyles.LowerCase) == FontStyles.LowerCase)
            {
                // If this character is uppercase, switch to lowercase.
                rune = rune.ToLower();
            }
            else if ((textConfiguration.m_fontStyleInternal & FontStyles.SmallCaps) == FontStyles.SmallCaps)
            {
                var oldUnicode = rune;
                rune = rune.ToUpper();
                if (rune != oldUnicode)
                {
                    smallCapsMultiplier = 0.8f;
                }
            }
        }
    }
}