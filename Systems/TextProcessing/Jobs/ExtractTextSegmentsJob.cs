using UnityEngine;
using TextMeshDOTS.RichText;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore.Text;


namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    public partial struct ExtractTextSegmentsJob : IJobEntity
    {
        public void Execute(Entity entity, 
            in TextBaseConfiguration textBaseConfiguration, 
            in DynamicBuffer<CalliByteRaw> calliBytesRawBuffer, 
            ref DynamicBuffer<CalliByte> calliBytesBuffer, 
            ref DynamicBuffer<TextSpan> textSpanBuffer,
            ref DynamicBuffer<FontMaterial> fontMaterialBuffer)
        {

            TextConfiguration textConfiguration = default;

            textConfiguration.Reset(in textBaseConfiguration, fontMaterialBuffer);
            var textSpans = new NativeList<TextSpan>(16, Allocator.Temp);
            var calliStringRaw = new CalliString(calliBytesRawBuffer);
            var calliString = new CalliString(calliBytesBuffer);
            calliBytesBuffer.Clear();
            textSpanBuffer.Clear();
            var rawCharacters = calliStringRaw.GetEnumerator();
            var characters = calliString.GetEnumerator();
            uint startIndex = 0;
            var previousRuneStartPosition = 0;
            while (rawCharacters.MoveNext())
            {
                var currentRune = rawCharacters.Current;
                if (currentRune == '<')  // '<'
                {
                    var segmentEnd = (uint)calliString.Length;
                    if (segmentEnd > startIndex)
                    {
                        textSpans.Add(new TextSpan
                        {
                            fontMaterialIndex = textConfiguration.m_currentFontMaterialIndex,
                            startIndex = startIndex,
                            endIndex = segmentEnd,
                            fontSize = textConfiguration.m_currentFontSize,
                            fontStyle = textConfiguration.m_fontStyleInternal,
                            lineJustification = textConfiguration.m_lineJustification,
                            color = textConfiguration.m_htmlColor,
                            monoSpacing = textConfiguration.m_monoSpacing,
                            cSpacing = textConfiguration.m_cSpacing,
                            fxScale = textConfiguration.m_fxScale,
                            fxRotationAngleCCW = textConfiguration.m_fxRotationAngleCCW,
                        });
                    }
                    startIndex = segmentEnd;
                    
                    if (RichTextParser.ValidateHtmlTag(in calliStringRaw, ref rawCharacters, ref fontMaterialBuffer, in textBaseConfiguration, ref textConfiguration))
                    {
                        previousRuneStartPosition = rawCharacters.NextRuneByteIndex;
                        continue;
                    }
                    else
                        rawCharacters.GotoByteIndex(previousRuneStartPosition);
                    
                }
                if ((textConfiguration.m_fontStyleInternal & FontStyles.UpperCase) == FontStyles.UpperCase)
                    calliString.Append(currentRune.ToUpper());
                else if ((textConfiguration.m_fontStyleInternal & FontStyles.LowerCase) == FontStyles.LowerCase)
                    calliString.Append(currentRune.ToLower());
                else
                    calliString.Append(currentRune);
                previousRuneStartPosition = rawCharacters.NextRuneByteIndex;
            }

            textSpans.Add(new TextSpan
            {                
                fontMaterialIndex = textConfiguration.m_currentFontMaterialIndex,
                startIndex = (uint)startIndex,
                endIndex = (uint)calliString.Length,
                fontSize = (int)textConfiguration.m_currentFontSize,
                fontStyle = textConfiguration.m_fontStyleInternal,
                lineJustification = textConfiguration.m_lineJustification,
                color = textConfiguration.m_htmlColor,
                monoSpacing = textConfiguration.m_monoSpacing,
                cSpacing = textConfiguration.m_cSpacing,
                fxScale = textConfiguration.m_fxScale,
                fxRotationAngleCCW = textConfiguration.m_fxRotationAngleCCW,
            });
            textSpanBuffer.AddRange(textSpans.AsArray());
        }        
    }
}
