using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using TextMeshDOTS.HarfBuzz;
using Unity.Burst.CompilerServices;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;


namespace TextMeshDOTS
{

    internal static class GlyphGeneration
    {
        internal static unsafe void CreateRenderGlyphs(ref FontTable fontTable,
                                                       ref GlyphTable glyphTable,
                                                       int threadIndex,
                                                       ref FontAssetArray fontAssetArray,
                                                       ref ComponentLookup<DynamicFontAsset> dynamicFontAssetsLookup,
                                                       ref ComponentLookup<FontAssetRef> fontAssetRefLookup,                                                       
                                                       ref DynamicBuffer<RenderGlyphOld> oldRenderGlyphs,
                                                       ref DynamicBuffer<RenderGlyph> renderGlyphs,
                                                       in DynamicBuffer<CalliByte> calliBytesBuffer,
                                                       in DynamicBuffer<GlyphOTF> glyphOTFBuffer,
                                                       in DynamicBuffer<XMLTag> xmlTagBuffer,
                                                       in TextBaseConfiguration textBaseConfiguration,
                                                       ref TextColorGradientArray textColorGradientArray)
        {
            //Debug.Log("CreateRenderGlyphs");
            oldRenderGlyphs.Clear();
            renderGlyphs.Clear();
            if (glyphOTFBuffer.IsEmpty)
                return;
            oldRenderGlyphs.Capacity = glyphOTFBuffer.Length; //2x speedup compared to allocation of individual items
            renderGlyphs.Capacity = glyphOTFBuffer.Length; //2x speedup compared to allocation of individual items
            
            var calliString = new CalliString(calliBytesBuffer);
            var characters = calliString.GetEnumerator();

            var fontAssetRefs = fontAssetArray.fontAssetRefs;
            var layoutConfig = new LayoutConfig(in textBaseConfiguration);

            XMLTag currentTag=default;
            int tagsCounter = 0;
            int nextSegmentEndID = xmlTagBuffer.Length > 0 ? xmlTagBuffer[tagsCounter].startID : calliString.Length;
            int cleanedSegmentLength = nextSegmentEndID - currentTag.endID;
            int richTextOffset = 0;
            int nextTagPositionInCleanedText = cleanedSegmentLength;
            //Debug.Log($"{currentTag.tagType} {cleanedSegmentLength} {nextTagPositionInCleanedText}");

            int lastWordStartCharacterGlyphIndex = 0;
            FixedList512Bytes<int> characterGlyphIndicesWithPreceedingSpacesInLine = default;
            int startOfLineGlyphIndex = 0;
            int lastCommittedStartOfLineGlyphIndex = -1;
            bool isFirstLine = true;
            bool isLineStart = true;
            float currentLineHeight = 0f;
            float ascentLineDelta = 0;
            float decentLineDelta = 0;
            float accumulatedVerticalOffset = 0f;
            float maxLineAscender = float.MinValue;
            float maxLineDescender = float.MaxValue;

            var currentFaceIndex = glyphOTFBuffer[0].glyphKey.faceIndex;
            Entity currentFontEntity = fontTable.faceIndexToFontEntityMap[currentFaceIndex];
            var currentFontAssetRef = fontAssetRefLookup[currentFontEntity];
            var currentFont = fontTable.GetOrCreateFont(currentFaceIndex, threadIndex);
            var currentFontSamplingPointSize = FontTextureSize.Normal.GetSamplingSize();
            currentFont.SetScale(currentFontSamplingPointSize, currentFontSamplingPointSize);
            currentFont.UpdateMetaData();

            // Calculate the scale of the font based on selected font size and sampling point size.
            // baseScale is calculated using the font asset assigned to the text object.            
            float baseScale = textBaseConfiguration.fontSize / currentFontSamplingPointSize * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);
            float currentElementScale = baseScale;
            float currentEmScale = textBaseConfiguration.fontSize * 0.01f * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);

            float topAnchor = GetTopAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);
            float bottomAnchor = GetBottomAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);

            Unicode.Rune currentRune, previousRune = Unicode.BadRune;//input text unicode

            for (int k = 0, length = glyphOTFBuffer.Length; k < length; k++)
            {
                var glyphOTF = glyphOTFBuffer[k];
                var glyphOTFFontEntity = fontTable.faceIndexToFontEntityMap[glyphOTF.glyphKey.faceIndex];

                var cluster = (int)glyphOTF.cluster; //cluster is char index in cleaned text = aligned with glyphOTF buffer
                if (currentFontEntity != glyphOTFFontEntity)
                {
                    currentFontEntity = glyphOTFFontEntity;
                    currentFontAssetRef = fontAssetRefLookup[currentFontEntity];
                    currentFont = fontTable.GetOrCreateFont(currentFaceIndex, threadIndex);
                    currentFont.SetScale(currentFontSamplingPointSize, currentFontSamplingPointSize);
                    currentFont.UpdateMetaData();
                }
                while (cluster >= nextTagPositionInCleanedText)
                {
                    if(tagsCounter < xmlTagBuffer.Length)
                    {                        
                        currentTag = xmlTagBuffer[tagsCounter++];
                        richTextOffset += currentTag.Length;
                        layoutConfig.Update(ref currentTag, textBaseConfiguration, ref textColorGradientArray);                       
                        nextSegmentEndID = tagsCounter < xmlTagBuffer.Length ? xmlTagBuffer[tagsCounter].startID - 1 : calliString.Length;
                        cleanedSegmentLength = nextSegmentEndID - currentTag.endID;                        
                        nextTagPositionInCleanedText = cluster + cleanedSegmentLength;
                        
                        //Debug.Log($"{currentTag.tagType} {cleanedSegmentLength} {nextTagPositionInCleanedText}");
                    }
                }

                // need to add richTextOffset to fetch correct char from richtext buffer. 
                // note: upper/lowercase is not applied in richtextBuffer (is only applied to cleaned text just before shaping)...should not cause any issues here
                characters.GotoByteIndex(richTextOffset + cluster); 
                currentRune = characters.Current;

                if (isFirstLine)
                    topAnchor = GetTopAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale, topAnchor);
                bottomAnchor = GetBottomAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale, bottomAnchor);

                #region Look up Character Data
                var glyphID = glyphTable.glyphHashToIdMap[glyphOTF.glyphKey];
                var glyphEntry = glyphTable.GetEntry(glyphID);
                //Debug.Log($"Render Glyph {glyphEntry.key.glyphIndex} from face {currentFaceIndex} using rect {glyphEntry.x} {glyphEntry.y} {glyphEntry.width} {glyphEntry.height} ({glyphEntry.PaddedWidth} {glyphEntry.PaddedHeight})");
                // review how to handle glyphOTF.codepoint = 0 (not defined glyph) which is retured for example for tab stop (9)
                // see here why: https://github.com/harfbuzz/harfbuzz/commit/81ef4f407d9c7bd98cf62cef951dc538b13442eb#commitcomment-9469767
                // should not be rendered, but xAdvance should be processed

                // Cache glyph metrics
                int x_bearing = glyphEntry.xBearing;
                int y_bearing   = glyphEntry.yBearing;
                int glyphHeight = glyphEntry.height;
                int glyphWidth  = glyphEntry.width;
                int padding = glyphEntry.padding;

                float adjustedScale = layoutConfig.m_currentFontSize / currentFontSamplingPointSize * (textBaseConfiguration.isOrthographic ? 1 : 0.1f);
                float elementAscentLine = currentFont.fontExtents.ascender;
                float elementDescentLine = currentFont.fontExtents.descender;

                //synthesize superscript and subscript redundant to opentype feature set during shaping.
                //only purpose is to simulate missing subscript glyphs, but unclear how to determine this
                float fontScaleMultiplier = 1;
                float m_subAndSupscriptOffset = 0;
                //if ((layoutConfiguration.m_fontStyles & FontStyles.Subscript) == FontStyles.Subscript && !currentRune.IsDigit())
                //{
                //    //Debug.Log($"{currentFont.subScriptEmXSize} {currentFont.subScriptEmYOffset} {adjustedScale}");
                //    fontScaleMultiplier = currentFont.subScriptEmXSize * adjustedScale;
                //    m_SubAndSupscriptOffset = -currentFont.subScriptEmYOffset * adjustedScale;
                //}
                //else if ((layoutConfiguration.m_fontStyles & FontStyles.Superscript) == FontStyles.Superscript && !currentRune.IsDigit())
                //{
                //    fontScaleMultiplier = currentFont.superScriptEmXSize * adjustedScale;
                //    m_SubAndSupscriptOffset = currentFont.superScriptEmYOffset * adjustedScale;
                //}

                currentElementScale = adjustedScale * fontScaleMultiplier;
                float baselineOffset = currentFont.baseLine * adjustedScale * fontScaleMultiplier;
                #endregion

                // Optimization to avoid calling this more than once per character.
                bool isWhiteSpace = currentRune.value <= 0xFFFF && currentRune.IsWhiteSpace();

                // Handle Mono Spacing
                #region Handle Mono Spacing
                float monoAdvance = 0;
                if (layoutConfig.m_monoSpacing != 0)
                {
                    monoAdvance =
                        (layoutConfig.m_monoSpacing / 2 - (glyphWidth / 2 + x_bearing) * currentElementScale);  // * (1 - charWidthAdjDelta);
                    layoutConfig.m_xAdvance += monoAdvance;
                }
                #endregion

                // Set Padding based on selected font style
                #region Handle Style Padding
                float boldSpacingAdjustment = 0;
                //if bold is requested and current font is not bold (=it has not been found), then simulate bold
                bool simulateBold = (layoutConfig.fontWeight >= FontWeight.Bold && currentFontAssetRef.weight < FontWeight.Bold);
                if (simulateBold)
                {
                    //Debug.Log($"Simulate Bold {currentFontAssetRef.weight} {(int)FontWeight.Bold}");
                    boldSpacingAdjustment = 7; //this is not a property of font so might as well just set it here
                }
                #endregion Handle Style Padding

                // Determine the position of the vertices of the Character or Sprite.
                #region Calculate Vertices Position
                var renderGlyph = new RenderGlyph
                {
                    glyphEntryId = glyphID,
                    arrayIndex = (uint)k
                };

                var renderGlyphOld = new RenderGlyphOld();
                renderGlyphOld.glyphID = glyphID;

                // the top left is used to position the bottom left and top right
                float2 topLeft;
                topLeft.x = layoutConfig.m_xAdvance + (x_bearing * layoutConfig.m_fxScale - padding + glyphOTF.xOffset) * currentElementScale;
                topLeft.y = baselineOffset + (y_bearing + padding + glyphOTF.yOffset) * currentElementScale + layoutConfig.m_baselineOffset + m_subAndSupscriptOffset;

                float2 bottomLeft;
                bottomLeft.x = topLeft.x;
                bottomLeft.y = topLeft.y - ((glyphHeight + padding * 2) * currentElementScale);

                float2 topRight;
                topRight.x = bottomLeft.x + (glyphWidth * layoutConfig.m_fxScale + padding * 2) * currentElementScale;
                topRight.y = topLeft.y;
                
                float2 bottomRight;
                bottomRight.x = topRight.x;
                bottomRight.y = bottomLeft.y;
                #endregion

                // We don't set up UVA here, as that is the atlas texture coordinates.
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

                renderGlyphOld.blUVB = blUVC;
                renderGlyphOld.tlUVB = tlUVC;
                renderGlyphOld.trUVB = trUVC;
                renderGlyphOld.brUVB = brUVC;
                
                renderGlyph.blUVB = blUVC;
                renderGlyph.tlUVB = tlUVC;
                renderGlyph.trUVB = trUVC;
                renderGlyph.brUVB = brUVC;
                #endregion

                #region Setup Color
                
                if (layoutConfig.useGradient) //&& !isColorGlyph)
                {
                    var gradient = layoutConfig.m_gradient;
                    renderGlyphOld.blColor = gradient.bottomLeft;
                    renderGlyphOld.tlColor = gradient.topLeft;
                    renderGlyphOld.trColor = gradient.topRight;
                    renderGlyphOld.brColor = gradient.bottomRight;
                    
                    var gradientBottomLeft = gradient.bottomLeft;
                    var gradientTopLeft = gradient.topLeft;
                    var gradientTopRight = gradient.topRight;
                    var gradientBottomRight = gradient.bottomRight;
                    renderGlyph.blColor = new half4(new half(gradientBottomLeft.r), new half(gradientBottomLeft.g), new half(gradientBottomLeft.b), new half(gradientBottomLeft.a));
                    renderGlyph.tlColor = new half4(new half(gradientTopLeft.r), new half(gradientTopLeft.g), new half(gradientTopLeft.b), new half(gradientTopLeft.a));
                    renderGlyph.trColor = new half4(new half(gradientTopRight.r), new half(gradientTopRight.g), new half(gradientTopRight.b), new half(gradientTopRight.a));
                    renderGlyph.brColor = new half4(new half(gradientBottomRight.r), new half(gradientBottomRight.g), new half(gradientBottomRight.b), new half(gradientBottomRight.a));
                    //if (m_ColorGradientPresetIsTinted)
                    //{
                    //    textInfo.textElementInfo[m_CharacterCount].vertexBottomLeft.color *= m_ColorGradientPreset.bottomLeft;
                    //    textInfo.textElementInfo[m_CharacterCount].vertexTopLeft.color *= m_ColorGradientPreset.topLeft;
                    //    textInfo.textElementInfo[m_CharacterCount].vertexTopRight.color *= m_ColorGradientPreset.topRight;
                    //    textInfo.textElementInfo[m_CharacterCount].vertexBottomRight.color *= m_ColorGradientPreset.bottomRight;
                    //}
                    //else
                    //{
                    //    textInfo.textElementInfo[m_CharacterCount].vertexBottomLeft.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.bottomLeft, vertexColor);
                    //    textInfo.textElementInfo[m_CharacterCount].vertexTopLeft.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.topLeft, vertexColor);
                    //    textInfo.textElementInfo[m_CharacterCount].vertexTopRight.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.topRight, vertexColor);
                    //    textInfo.textElementInfo[m_CharacterCount].vertexBottomRight.color = TextGeneratorUtilities.MinAlpha(m_ColorGradientPreset.bottomRight, vertexColor);
                    //}
                }
                else
                {
                    renderGlyphOld.blColor = layoutConfig.m_htmlColor;
                    renderGlyphOld.tlColor = layoutConfig.m_htmlColor;
                    renderGlyphOld.trColor = layoutConfig.m_htmlColor;
                    renderGlyphOld.brColor = layoutConfig.m_htmlColor;

                    var layoutHtmlColor = new half4(new half(layoutConfig.m_htmlColor.r), new half(layoutConfig.m_htmlColor.g), new half(layoutConfig.m_htmlColor.b), new half(layoutConfig.m_htmlColor.a));
                    renderGlyph.blColor = layoutHtmlColor;
                    renderGlyph.tlColor = layoutHtmlColor;
                    renderGlyph.trColor = layoutHtmlColor;
                    renderGlyph.brColor = layoutHtmlColor;
                }
                #endregion

                #region Pack Scale into renderGlyph.scale
                var scale = layoutConfig.m_currentFontSize;
                if (simulateBold)
                    scale *= -1;

                renderGlyphOld.scale = scale;
                renderGlyph.scale = scale;
                #endregion

                // Check if we need to Shear the rectangles for Italic styles
                #region Handle Italic & Shearing
                float bottomShear = 0f;
                //if italic is requested and current font is not italic (=it has not been found), then simulate italic
                bool simulateItalic = (layoutConfig.m_fontStyles & FontStyles.Italic) == FontStyles.Italic && !currentFontAssetRef.isItalic;
                if (simulateItalic)
                {
                    //Debug.Log($"Simulate Italic {currentFontAssetRef.isItalic}");
                    // Shift Top vertices forward by half (Shear Value * height of character) and Bottom vertices back by same amount.
                    var italicsStyleSlant = 35; //this is not a property of font so might as well just set it here
                    float shear_value = italicsStyleSlant * 0.01f;
                    float midPoint = ((currentFont.capHeight - (currentFont.baseLine + layoutConfig.m_baselineOffset + m_subAndSupscriptOffset)) / 2) * fontScaleMultiplier;
                    float topShear = shear_value * ((y_bearing + padding - midPoint) * currentElementScale);
                    bottomShear = shear_value *
                                        ((y_bearing - glyphHeight - padding - midPoint) *
                                         currentElementScale);

                    topLeft.x += topShear;
                    bottomLeft.x += bottomShear;
                    topRight.x += topShear;
                    bottomRight.x += bottomShear;

                    renderGlyphOld.shear = topLeft.x - bottomLeft.x;
                }
                #endregion Handle Italics & Shearing

                // Needs to be done before rotation as it affects the position of the vertices and the old glyph can't handle that
#region Store vertex information for the character or sprite of the old glyphs.
                renderGlyphOld.trPosition = topRight;
                renderGlyphOld.blPosition = bottomLeft;
#endregion
                
                // Handle Character FX Rotation
                #region Handle Character FX Rotation

                float rotation = math.radians(layoutConfig.m_fxRotationAngleCCW_degree);
                renderGlyphOld.rotationCCW = rotation;
                if (math.abs(rotation) > 0.0001f)
                {
                    float2 pivot = (topLeft + bottomRight) * 0.5f;
                    float sinRotation = math.sin(rotation);
                    float cosRotation = math.cos(rotation);

                    topLeft = RotatePoint(topLeft, pivot, sinRotation, cosRotation);
                    bottomLeft = RotatePoint(bottomLeft, pivot, sinRotation, cosRotation);
                    topRight = RotatePoint(topRight, pivot, sinRotation, cosRotation);
                    bottomRight = RotatePoint(bottomRight, pivot, sinRotation, cosRotation);
                }
                #endregion

                #region Store vertex information for the character or sprite.
                renderGlyphOld.trPosition = topRight;
                renderGlyphOld.blPosition = bottomLeft;
                
                renderGlyph.trPosition = topRight;
                renderGlyph.tlPosition = topLeft;
                renderGlyph.blPosition = bottomLeft;
                renderGlyph.brPosition = bottomRight;
                if (Hint.Likely(currentRune.value != 10)) //do not render LF 
                {
                    oldRenderGlyphs.Add(renderGlyphOld);
                    renderGlyphs.Add(renderGlyph);
                }
                #endregion

                // Compute text metrics
                #region Compute Ascender & Descender values
                // Element Ascender in line space
                float elementAscender = elementAscentLine * currentElementScale + layoutConfig.m_baselineOffset + m_subAndSupscriptOffset;

                // Element Descender in line space
                float elementDescender = elementDescentLine * currentElementScale + layoutConfig.m_baselineOffset + m_subAndSupscriptOffset;

                float adjustedAscender = elementAscender;
                float adjustedDescender = elementDescender;

                // Max line ascender and descender in line space
                if (isLineStart || isWhiteSpace == false)
                {
                    // Special handling for Superscript and Subscript where we use the unadjusted line ascender and descender
                    if (m_subAndSupscriptOffset != 0) //To-Do: review (also voffset affecting m_baselineOffset), effect not clear. 
                    {
                        adjustedAscender = math.max((elementAscender - m_subAndSupscriptOffset) / fontScaleMultiplier, adjustedAscender);
                        adjustedDescender = math.min((elementDescender - m_subAndSupscriptOffset) / fontScaleMultiplier, adjustedDescender);
                    }
                    maxLineAscender = math.max(adjustedAscender, maxLineAscender);
                    maxLineDescender = math.min(adjustedDescender, maxLineDescender);
                }
                #endregion

                #region XAdvance, Tabulation & Stops
                if (currentRune.value == 9)
                {
                    float tabSize = currentFont.TabAdvance() * currentElementScale;
                    float tabs = math.ceil(layoutConfig.m_xAdvance / tabSize) * tabSize;
                    layoutConfig.m_xAdvance = tabs > layoutConfig.m_xAdvance ? tabs : layoutConfig.m_xAdvance + tabSize;
                }
                else if (layoutConfig.m_monoSpacing != 0)
                {
                    float monoAdjustment = layoutConfig.m_monoSpacing - monoAdvance;
                    layoutConfig.m_xAdvance += (monoAdjustment + layoutConfig.m_cSpacing);
                    if (isWhiteSpace || currentRune.value == 0x200B)
                        layoutConfig.m_xAdvance += textBaseConfiguration.wordSpacing * currentEmScale;
                }
                else
                {
                    layoutConfig.m_xAdvance += (glyphOTF.xAdvance * layoutConfig.m_fxScale) * currentElementScale +
                                boldSpacingAdjustment * currentEmScale + layoutConfig.m_cSpacing;

                    if (isWhiteSpace || currentRune.value == 0x200B)
                        layoutConfig.m_xAdvance += textBaseConfiguration.wordSpacing * currentEmScale;
                }
                #endregion XAdvance, Tabulation & Stops

                #region Check for Line Feed and Last Character
                if (isLineStart)
                    isLineStart = false;
                currentLineHeight = (currentFont.fontExtents.ascender - currentFont.fontExtents.descender) * baseScale;
                ascentLineDelta = maxLineAscender - currentFont.fontExtents.ascender * baseScale;
                decentLineDelta = currentFont.fontExtents.descender * baseScale - maxLineDescender;
                //if (currentRune.value == 10 || currentRune.value == 11 || currentRune.value == 0x03 || currentRune.value == 0x2028 ||
                //    currentRune.value == 0x2029 || textConfiguration.m_characterCount == calliString.Length - 1)
                if (currentRune.value == 10)
                {
                    var oldGlyphsLine = oldRenderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, oldRenderGlyphs.Length - startOfLineGlyphIndex);
                    var glyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
                    var overrideMode = layoutConfig.m_lineJustification;
                    if ((overrideMode) == HorizontalAlignmentOptions.Justified)
                    {
                        // Don't perform justified spacing for the last line in the paragraph.
                        overrideMode = HorizontalAlignmentOptions.Left;
                    }
                    ApplyHorizontalAlignmentToGlyphs(ref oldGlyphsLine,
                                                     ref glyphsLine,
                                                     ref characterGlyphIndicesWithPreceedingSpacesInLine,
                                                     textBaseConfiguration.maxLineWidth,
                                                     overrideMode);
                    startOfLineGlyphIndex = oldRenderGlyphs.Length;
                    if (!isFirstLine)
                    {
                        accumulatedVerticalOffset += currentLineHeight + ascentLineDelta;
                        if (lastCommittedStartOfLineGlyphIndex != startOfLineGlyphIndex)
                        {
                            ApplyVerticalOffsetToGlyphs(ref oldGlyphsLine, ref glyphsLine, accumulatedVerticalOffset);
                            lastCommittedStartOfLineGlyphIndex = startOfLineGlyphIndex;
                        }
                    }
                    accumulatedVerticalOffset += decentLineDelta;
                    //apply user configurable line and paragraph spacing
                    accumulatedVerticalOffset +=
                        (textBaseConfiguration.lineSpacing + (currentRune.value == 10 || currentRune.value == 0x2029 ? textBaseConfiguration.paragraphSpacing : 0)) * currentEmScale;

                    //reset line status
                    maxLineAscender = float.MinValue;
                    maxLineDescender = float.MaxValue;

                    isFirstLine = false;
                    isLineStart = true;
                    bottomAnchor = GetBottomAnchorForConfig(ref currentFont, textBaseConfiguration.verticalAlignment, baseScale);

                    layoutConfig.m_xAdvance = layoutConfig.m_tagIndent;
                    previousRune = currentRune;
                    continue;
                }
                #endregion

                #region Word Wrapping
                // Handle word wrap
                if (textBaseConfiguration.maxLineWidth < float.MaxValue &&
                    textBaseConfiguration.maxLineWidth > 0 &&
                    layoutConfig.m_xAdvance > textBaseConfiguration.maxLineWidth)
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
                    }

                    var yOffsetChange = 0f;  //font.lineHeight * currentElementScale;
                    // TODO this line should be later replaced with renderGlyphs
                    var xOffsetChange = oldRenderGlyphs[lastWordStartCharacterGlyphIndex].blPosition.x - bottomShear - layoutConfig.m_tagIndent;
                    if (xOffsetChange > 0 && !dropSpace)  // Always allow one visible character
                    {
                        // Finish line based on alignment
                        var oldGlyphsLine = oldRenderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex,
                                                                                  lastWordStartCharacterGlyphIndex - startOfLineGlyphIndex);
                        var glyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex,
                            lastWordStartCharacterGlyphIndex - startOfLineGlyphIndex);
                        ApplyHorizontalAlignmentToGlyphs(ref oldGlyphsLine,
                                                         ref glyphsLine,
                                                         ref characterGlyphIndicesWithPreceedingSpacesInLine,
                                                         textBaseConfiguration.maxLineWidth,
                                                         layoutConfig.m_lineJustification);

                        if (!isFirstLine)
                        {
                            accumulatedVerticalOffset += currentLineHeight + ascentLineDelta;
                            ApplyVerticalOffsetToGlyphs(ref oldGlyphsLine, ref glyphsLine, accumulatedVerticalOffset);
                            lastCommittedStartOfLineGlyphIndex = startOfLineGlyphIndex;
                        }
                        accumulatedVerticalOffset += decentLineDelta;  // Todo: Delta should be computed per glyph
                        //apply user configurable line and paragraph spacing
                        accumulatedVerticalOffset += textBaseConfiguration.lineSpacing * currentEmScale;

                        //reset line status
                        maxLineAscender = float.MinValue;
                        maxLineDescender = float.MaxValue;

                        startOfLineGlyphIndex = lastWordStartCharacterGlyphIndex;
                        isLineStart = true;
                        isFirstLine = false;

                        layoutConfig.m_xAdvance -= xOffsetChange;

                        // Adjust the vertices of the previous render glyphs in the word
                        var glyphPtr = (RenderGlyphOld*)oldRenderGlyphs.GetUnsafePtr();
                        for (int i = lastWordStartCharacterGlyphIndex; i < oldRenderGlyphs.Length; i++)
                        {
                            glyphPtr[i].blPosition.y -= yOffsetChange;
                            glyphPtr[i].blPosition.x -= xOffsetChange;
                            glyphPtr[i].trPosition.y -= yOffsetChange;
                            glyphPtr[i].trPosition.x -= xOffsetChange;
                        }
                        var glyphPtrNew = (RenderGlyph*)renderGlyphs.GetUnsafePtr();
                        for (int i = lastWordStartCharacterGlyphIndex; i < renderGlyphs.Length; i++)
                        {
                            glyphPtrNew[i].blPosition.y -= yOffsetChange;
                            glyphPtrNew[i].blPosition.x -= xOffsetChange;
                            glyphPtrNew[i].tlPosition.y -= yOffsetChange;
                            glyphPtrNew[i].tlPosition.x -= xOffsetChange;
                            glyphPtrNew[i].trPosition.y -= yOffsetChange;
                            glyphPtrNew[i].trPosition.x -= xOffsetChange;
                            glyphPtrNew[i].brPosition.y -= yOffsetChange;
                            glyphPtrNew[i].brPosition.x -= xOffsetChange;
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
                    lastWordStartCharacterGlyphIndex = oldRenderGlyphs.Length;
                }
                #endregion
                previousRune = currentRune;
            }

            var oldFinalGlyphsLine = oldRenderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, oldRenderGlyphs.Length - startOfLineGlyphIndex);
            var finalGlyphsLine = renderGlyphs.AsNativeArray().GetSubArray(startOfLineGlyphIndex, renderGlyphs.Length - startOfLineGlyphIndex);
            {
                var overrideMode = layoutConfig.m_lineJustification;
                if (overrideMode == HorizontalAlignmentOptions.Justified)
                {
                    // Don't perform justified spacing for the last line.
                    overrideMode = HorizontalAlignmentOptions.Left;
                }
                ApplyHorizontalAlignmentToGlyphs(ref oldFinalGlyphsLine, ref finalGlyphsLine, ref characterGlyphIndicesWithPreceedingSpacesInLine, textBaseConfiguration.maxLineWidth, overrideMode);
                if (!isFirstLine)
                {
                    accumulatedVerticalOffset += currentLineHeight;
                    ApplyVerticalOffsetToGlyphs(ref oldFinalGlyphsLine, ref finalGlyphsLine, accumulatedVerticalOffset);
                }
            }
            isFirstLine = false;
            ApplyVerticalAlignmentToGlyphs(ref oldRenderGlyphs, ref renderGlyphs, topAnchor, bottomAnchor, accumulatedVerticalOffset, textBaseConfiguration.verticalAlignment);
        }
        
        static float2 RotatePoint(float2 point, float2 pivot, float sin, float cos)
        {
            float2 translated = point - pivot;
            return new float2(
                translated.x * cos - translated.y * sin,
                translated.x * sin + translated.y * cos
            ) + pivot;
        }

        static float GetTopAnchorForConfig(ref Font font, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.TopBase: return 0f;
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.TopAscent: return baseScale * math.max(font.fontExtents.ascender - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopDescent: return baseScale * math.min(font.fontExtents.descender - font.baseLine, oldValue);
                case VerticalAlignmentOptions.TopCap: return baseScale * math.max(font.capHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.TopMean: return baseScale * math.max(font.xHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static float GetBottomAnchorForConfig(ref Font font, VerticalAlignmentOptions verticalMode, float baseScale, float oldValue = float.PositiveInfinity)
        {
            bool replace = oldValue == float.PositiveInfinity;
            switch (verticalMode)
            {
                case VerticalAlignmentOptions.BottomBase: return 0f;
                case VerticalAlignmentOptions.BottomAscent: return baseScale * math.max(font.fontExtents.ascender - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                case VerticalAlignmentOptions.BottomDescent: return baseScale * math.min(font.fontExtents.descender - font.baseLine, oldValue);
                case VerticalAlignmentOptions.BottomCap: return baseScale * math.max(font.capHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                case VerticalAlignmentOptions.BottomMean: return baseScale * math.max(font.xHeight - font.baseLine, math.select(oldValue, float.NegativeInfinity, replace));
                default: return 0f;
            }
        }

        static unsafe void ApplyHorizontalAlignmentToGlyphs(ref NativeArray<RenderGlyphOld> oldGlyphs,
                                                            ref NativeArray<RenderGlyph> renderGlyphs,
                                                            ref FixedList512Bytes<int> characterGlyphIndicesWithPreceedingSpacesInLine,
                                                            float width,
                                                            HorizontalAlignmentOptions alignMode)
        {
            if ((alignMode) == HorizontalAlignmentOptions.Left)
            {
                characterGlyphIndicesWithPreceedingSpacesInLine.Clear();
                return;
            }

            var oldGlyphsPtr = (RenderGlyphOld*)oldGlyphs.GetUnsafePtr();
            var glyphsPtr = (RenderGlyph*)renderGlyphs.GetUnsafePtr();
            if (alignMode == HorizontalAlignmentOptions.Center)
            {
                float oldOffset = oldGlyphsPtr[oldGlyphs.Length - 1].trPosition.x / 2f;
                for (int i = 0; i < oldGlyphs.Length; i++)
                {
                    oldGlyphsPtr[i].blPosition.x -= oldOffset;
                    oldGlyphsPtr[i].trPosition.x -= oldOffset;
                }
                float offset = glyphsPtr[renderGlyphs.Length - 1].trPosition.x / 2f;
                for (int i = 0; i < renderGlyphs.Length; i++)
                {
                    glyphsPtr[i].blPosition.x -= offset;
                    glyphsPtr[i].trPosition.x -= offset;
                    glyphsPtr[i].tlPosition.x -= offset;
                    glyphsPtr[i].brPosition.x -= offset;
                }
            }
            else if (alignMode == HorizontalAlignmentOptions.Right)
            {
                float oldOffset = oldGlyphsPtr[oldGlyphs.Length - 1].trPosition.x;
                for (int i = 0; i < oldGlyphs.Length; i++)
                {
                    oldGlyphsPtr[i].blPosition.x -= oldOffset;
                    oldGlyphsPtr[i].trPosition.x -= oldOffset;
                }
                float offset = glyphsPtr[renderGlyphs.Length - 1].trPosition.x;
                for (int i = 0; i < renderGlyphs.Length; i++)
                {
                    glyphsPtr[i].blPosition.x -= offset;
                    glyphsPtr[i].trPosition.x -= offset;
                    glyphsPtr[i].tlPosition.x -= offset;
                    glyphsPtr[i].brPosition.x -= offset;
                }
            }
            else  // Justified
            {
                float nudgePerSpace = (width - oldGlyphsPtr[oldGlyphs.Length - 1].trPosition.x) / characterGlyphIndicesWithPreceedingSpacesInLine.Length;
                float accumulatedOffset = 0f;
                int indexInIndices = 0;
                for (int i = 0; i < oldGlyphs.Length; i++)
                {
                    while (indexInIndices < characterGlyphIndicesWithPreceedingSpacesInLine.Length &&
                           characterGlyphIndicesWithPreceedingSpacesInLine[indexInIndices] == i)
                    {
                        accumulatedOffset += nudgePerSpace;
                        indexInIndices++;
                    }

                    oldGlyphsPtr[i].blPosition.x += accumulatedOffset;
                    oldGlyphsPtr[i].trPosition.x += accumulatedOffset;
                    
                    glyphsPtr[i].blPosition.x += accumulatedOffset;
                    glyphsPtr[i].trPosition.x += accumulatedOffset;
                    glyphsPtr[i].tlPosition.x += accumulatedOffset;
                    glyphsPtr[i].brPosition.x += accumulatedOffset;
                }
            }
            characterGlyphIndicesWithPreceedingSpacesInLine.Clear();
        }

        static unsafe void ApplyVerticalOffsetToGlyphs(ref NativeArray<RenderGlyphOld> oldGlyphs, ref NativeArray<RenderGlyph> glyphs, float accumulatedVerticalOffset)
        {
            for (int i = 0; i < oldGlyphs.Length; i++)
            {
                var glyph = oldGlyphs[i];
                glyph.blPosition.y -= accumulatedVerticalOffset;
                glyph.trPosition.y -= accumulatedVerticalOffset;
                oldGlyphs[i] = glyph;
            }
            for (int i = 0; i < glyphs.Length; i++)
            {
                var glyph = glyphs[i];
                glyph.blPosition.y -= accumulatedVerticalOffset;
                glyph.tlPosition.y -= accumulatedVerticalOffset;
                glyph.trPosition.y -= accumulatedVerticalOffset;
                glyph.brPosition.y -= accumulatedVerticalOffset;
                glyphs[i] = glyph;
            }
        }

        static unsafe void ApplyVerticalAlignmentToGlyphs(ref DynamicBuffer<RenderGlyphOld> oldGlyphs,
                                                          ref DynamicBuffer<RenderGlyph> glyphs,
                                                          float topAnchor,
                                                          float bottomAnchor,
                                                          float accumulatedVerticalOffset,
                                                          VerticalAlignmentOptions alignMode)
        {
            var oldGlyphsPtr = (RenderGlyphOld*)oldGlyphs.GetUnsafePtr();
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
                        for (int i = 0; i < oldGlyphs.Length; i++)
                        {
                            oldGlyphsPtr[i].blPosition.y -= topAnchor;
                            oldGlyphsPtr[i].trPosition.y -= topAnchor;
                        }
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            glyphsPtr[i].blPosition.y -= topAnchor;
                            glyphsPtr[i].tlPosition.y -= topAnchor;
                            glyphsPtr[i].trPosition.y -= topAnchor;
                            glyphsPtr[i].brPosition.y -= topAnchor;
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
                        for (int i = 0; i < oldGlyphs.Length; i++)
                        {
                            oldGlyphsPtr[i].blPosition.y += offset;
                            oldGlyphsPtr[i].trPosition.y += offset;
                        }
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            glyphsPtr[i].blPosition.y += offset;
                            glyphsPtr[i].tlPosition.y += offset;
                            glyphsPtr[i].trPosition.y += offset;
                            glyphsPtr[i].brPosition.y += offset;
                        }
                        break;
                    }
                case VerticalAlignmentOptions.MiddleTopAscentToBottomDescent:
                    {
                        float fullHeight = accumulatedVerticalOffset - bottomAnchor + topAnchor;
                        float offset = fullHeight / 2f;
                        for (int i = 0; i < oldGlyphs.Length; i++)
                        {
                            oldGlyphsPtr[i].blPosition.y += offset;
                            oldGlyphsPtr[i].trPosition.y += offset;
                        }
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            glyphsPtr[i].blPosition.y += offset;
                            glyphsPtr[i].tlPosition.y += offset;
                            glyphsPtr[i].trPosition.y += offset;
                            glyphsPtr[i].brPosition.y += offset;
                        }
                        break;
                    }
            }
        }
    }
}