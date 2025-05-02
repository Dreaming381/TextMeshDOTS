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

    /// <summary>
    /// The backing memory for a GlyphMapper struct. Cast to a GlyphMapper
    /// to get a mapping of source string to RenderGlyph to post-process the glyphs.
    /// Usage: ReadOnly
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct GlyphMappingElement : IBufferElementData
    {
        public int2 element;
    }

    /// <summary>
    /// The mask used to command glyph generation which mappings to generate.
    /// Generating more mappings has a slightly large performance cost and a
    /// potentially significant memory cost.
    /// </summary>
    public struct GlyphMappingMask : IComponentData
    {
        public enum WriteMask : byte
        {
            None = 0,
            Line = 0x1,
            Word = 0x2,
            CharNoTags = 0x4,
            CharWithTags = 0x8,
            Byte = 0x10,
        }

        public WriteMask mask;
    }
}