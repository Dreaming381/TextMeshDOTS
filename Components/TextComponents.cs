using TextMeshDOTS.RichText;
using Unity.Entities;
using Unity.Mathematics;


namespace TextMeshDOTS
{    

    /// <summary>
    /// The raw byte element as part of the text string.
    /// Cast to CalliString to read  /write.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CalliByte : IBufferElementData
    {
        public byte element;
    }
    [InternalBufferCapacity(0)]
    public struct XMLTag : IBufferElementData
    {
        public TagType tagType;
        public bool isClosing;
        public int startID; //start position raw text
        public int endID;   //start position raw text
        public int Length => endID + 1 - startID;
        public TagValue value;
        public XMLTag(bool dummy)
        {
            tagType = TagType.Unknown;
            isClosing = false;
            startID = -1;
            endID = -1;
            value = new TagValue();
            value.type = TagValueType.None;
            value.unit = TagUnitType.Pixels;            
        }
    }
}