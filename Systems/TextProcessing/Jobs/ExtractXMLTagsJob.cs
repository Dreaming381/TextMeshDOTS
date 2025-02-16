using TextMeshDOTS.RichText;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Burst.Intrinsics;


namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    public partial struct ExtractTagsJob : IJobChunk
    {
        public BufferTypeHandle<CalliByte> calliByteHandle;
        public BufferTypeHandle<XMLTag> xmlTagHandle; 
        [ReadOnly] public BufferTypeHandle<CalliByteRaw> calliByteRawHandle;

        public uint lastSystemVersion;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!(chunk.DidChange(ref calliByteRawHandle, lastSystemVersion)))
                return;

            //Debug.Log("Extract text segments job");
            var calliBytesBuffers = chunk.GetBufferAccessor(ref calliByteHandle);
            var calliBytesRawBuffers = chunk.GetBufferAccessor(ref calliByteRawHandle);
            var xmlTagBuffers = chunk.GetBufferAccessor(ref xmlTagHandle);

            FixedString128Bytes m_htmlTag = new FixedString128Bytes();
            var tmpTags = new NativeList<XMLTag>(16, Allocator.Temp);
            for (int indexInChunk = 0; indexInChunk < chunk.Count; indexInChunk++)
            {
                var calliBytesBuffer = calliBytesBuffers[indexInChunk];
                var calliBytesRawBuffer = calliBytesRawBuffers[indexInChunk];
                calliBytesBuffer.Clear();
                calliBytesBuffer.Capacity = calliBytesRawBuffer.Length;//2x speedup compared to allocating for each element.  Might overallocate a bit when text uses a lot of richtext tags
                var xmlTags = xmlTagBuffers[indexInChunk];
                
                var calliStringRaw = new CalliString(calliBytesRawBuffer);
                var calliString = new CalliString(calliBytesBuffer);
                calliBytesBuffer.Clear();
                xmlTags.Clear();
                var rawCharacters = calliStringRaw.GetEnumerator();
                var previousRuneStartPosition = 0;
                FontStyles currentFontStyle=default;
                int utf8Count = 0;
                while (rawCharacters.MoveNext())
                {
                    var currentRune = rawCharacters.Current;
                    if (currentRune == '<')  // '<'
                    {
                        tmpTags.Clear();
                        if (RichTextParser.GetTag(in calliStringRaw, ref rawCharacters, utf8Count, ref xmlTags, tmpTags, ref m_htmlTag))
                        {
                            previousRuneStartPosition = rawCharacters.NextRuneByteIndex;
                            continue;
                        }
                        else
                            rawCharacters.GotoByteIndex(previousRuneStartPosition);
                    }

                    //best to change case here when building cleaned text buffer
                    if (tmpTags.Length > 0)
                    {
                        var lastTag = tmpTags[^1];
                        var lastTagType = lastTag.tagType;
                        switch (lastTagType)
                        {
                            case TagType.AllCaps:
                            case TagType.Uppercase:
                                {
                                    if (lastTag.isClosing)
                                        currentFontStyle &= ~FontStyles.UpperCase;
                                    else
                                        currentFontStyle |= FontStyles.UpperCase;
                                }
                                break;
                            case TagType.Lowercase:
                                {
                                    if (lastTag.isClosing)
                                        currentFontStyle &= ~FontStyles.LowerCase;
                                    else
                                        currentFontStyle |= FontStyles.LowerCase;
                                }
                                break;
                        }
                    }
                    if ((currentFontStyle & FontStyles.UpperCase) == FontStyles.UpperCase)
                        calliString.Append(currentRune.ToUpper());
                    else if ((currentFontStyle & FontStyles.LowerCase) == FontStyles.LowerCase)
                        calliString.Append(currentRune.ToLower());
                    else
                        calliString.Append(currentRune);
                    utf8Count += currentRune.LengthInUtf8Bytes();
                    previousRuneStartPosition = rawCharacters.NextRuneByteIndex;
                }
                //for (int i = 0; i<tags.Length; i++)
                //{
                //    var tag= tags[i];
                //    if(tag.tagType==TagType.Unknown)
                //        continue;
                //    if (tag.isClosing)
                //        Debug.Log($"{tag.position} {tag.tagType} isclosing? {tag.isClosing}");
                //    else
                //    {
                //        if (tag.value.type == TagValueType.NumericalValue)
                //            Debug.Log($"{tag.position} {tag.tagType} {tag.value.NumericalValue} {tag.value.unit}");
                //        else if (tag.value.type == TagValueType.ColorValue)
                //            Debug.Log($"{tag.position} {tag.tagType} {tag.value.ColorValue}");
                //        else if (tag.value.type == TagValueType.StringValue)
                //        {
                //            calliStringRaw.GetSubString(ref textConfiguration.m_htmlTag, tag.value.valueStart, tag.value.valueLength);
                //            Debug.Log($"{tag.position} {tag.tagType} {tag.value.type} {textConfiguration.m_htmlTag}");
                //        }
                //        else
                //            Debug.Log($"{tag.position} {tag.tagType} {tag.value.type}");
                //    }
                //}
            }
        }
    }    
}
