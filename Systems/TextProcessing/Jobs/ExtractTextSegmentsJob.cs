using TextMeshDOTS;
using TextMeshDOTS.RichText;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    public partial struct ExtractTextSegmentsJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<FontBlobReference> fontBlobReferenceLookup;
        public void Execute(in TextBaseConfiguration textBaseConfiguration, in FontBlobReference fontBlobReference, in DynamicBuffer<CalliByteRaw> calliBytesRawBuffer, ref DynamicBuffer<CalliByte> calliBytesBuffer, ref DynamicBuffer<TextSpan> textSpanBuffer)
        {
            FontMaterialSet fontMaterialSet = default;
            TextConfiguration textConfiguration = default;
            fontMaterialSet.Initialize(fontBlobReference.blob);

            textConfiguration.Reset(in textBaseConfiguration, ref fontMaterialSet);
            var textSpans = new NativeList<TextSpan>(16, Allocator.Temp);
            var calliStringRaw = new CalliString(calliBytesRawBuffer);
            var calliString = new CalliString(calliBytesBuffer);
            calliBytesBuffer.Clear();
            textSpanBuffer.Clear();
            var rawCharacters = calliStringRaw.GetEnumerator();
            var characters = calliString.GetEnumerator();
            int startIndex = 0;
            while (rawCharacters.MoveNext())
            {
                var currentRune = rawCharacters.Current;
                if (currentRune == '<')  // '<'
                {
                    //var length = calliString.Length - startIndex;
                    if (calliString.Length > startIndex)
                    {
                        textSpans.Add(new TextSpan
                        {
                            fontMaterialIndex = textConfiguration.m_currentFontMaterialIndex,
                            startIndex = (uint)startIndex,
                            endIndex = (uint)calliString.Length,
                            fontSize = (int)textConfiguration.m_currentFontSize,
                            fontStyle = textConfiguration.m_fontStyleInternal,
                            fontWeight = textConfiguration.m_fontWeightInternal,
                            lineJustification = textConfiguration.m_lineJustification,
                            color = textConfiguration.m_htmlColor,                            
                            monoSpacing = textConfiguration.m_monoSpacing,
                            cSpacing = textConfiguration.m_cSpacing,
                            fxScale = textConfiguration.m_fxScale,
                            fxRotationAngleCCW = textConfiguration.m_fxRotationAngleCCW,
                            italicAngle = textConfiguration.m_italicAngle,
                        });
                    }
                    startIndex = calliString.Length;
                    if (RichTextParser.ValidateHtmlTag(in calliStringRaw, ref rawCharacters, ref fontMaterialSet, in textBaseConfiguration, ref textConfiguration))
                    {
                        continue;
                    }
                }
                if ((textConfiguration.m_fontStyleInternal & FontStyles.UpperCase) == FontStyles.UpperCase)
                    calliString.Append(currentRune.ToUpper());
                else if ((textConfiguration.m_fontStyleInternal & FontStyles.LowerCase) == FontStyles.LowerCase)
                    calliString.Append(currentRune.ToLower());
                else
                    calliString.Append(currentRune);
            }

            textSpans.Add(new TextSpan
            {                
                fontMaterialIndex = textConfiguration.m_currentFontMaterialIndex,
                startIndex = (uint)startIndex,
                endIndex = (uint)calliString.Length + 1, //make last TextSpan 1 longer to ensure GlyphGeneration.CreateRenderGlyphs does not try to load next TextSpan on last index.
                fontSize = (int)textConfiguration.m_currentFontSize,
                fontStyle = textConfiguration.m_fontStyleInternal,
                fontWeight = textConfiguration.m_fontWeightInternal,
                lineJustification = textConfiguration.m_lineJustification,
                color = textConfiguration.m_htmlColor,
                monoSpacing = textConfiguration.m_monoSpacing,
                cSpacing = textConfiguration.m_cSpacing,
                fxScale = textConfiguration.m_fxScale,
                fxRotationAngleCCW = textConfiguration.m_fxRotationAngleCCW,
                italicAngle = textConfiguration.m_italicAngle,
            });
            textSpanBuffer.AddRange(textSpans.AsArray());
        }
    }
}
