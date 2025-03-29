using System;
using TextMeshDOTS.RichText;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS
{
    /// <summary>
    /// Definition of color gradients
    /// </summary>
    [Serializable]
    public struct TextMeshDOTSColorGradient
    {
        [SerializeField]
        public string name;
        [SerializeField]
        public ColorGradientMode colorMode;
        [SerializeField]
        public Color topLeft;
        [SerializeField]
        public Color topRight;
        [SerializeField]
        public Color bottomLeft;
        [SerializeField]
        public Color bottomRight;
    }
    /// <summary>
    /// Definition of color gradients
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TextColorGradient : IBufferElementData
    {
        public int nameHash;
        public Color32 topLeft;
        public Color32 topRight;
        public Color32 bottomLeft;
        public Color32 bottomRight;
    }

    /// <summary>
    /// The base settings of the text before any rich text tags or animations are applied.
    /// Usage: ReadWrite
    /// </summary>
    public struct TextBaseConfiguration : IComponentData
    {
        public float fontSize;        
        public Color32 color;

        public FontStyles fontStyles; //readout of bold style only during authoring, otherwise this library will use fontweight (selectable via xml tags)
        public FontWeight fontWeight; //selectable via xml tags
        public float fontWidth;   //selectable via xml tags

        public float maxLineWidth;
        public float wordSpacing;
        public float lineSpacing;
        public float paragraphSpacing;
        public HorizontalAlignmentOptions lineJustification;
        public VerticalAlignmentOptions verticalAlignment;
        public bool isOrthographic;
    }    

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

    /// <summary>
    /// Horizontal text alignment options.
    /// </summary>
    public enum HorizontalAlignmentOptions : byte
    {
        Left,
        Center,
        Right,
        Justified,
        Flush,
        Geometry
    }

    /// <summary>
    /// Vertical text alignment options.
    /// </summary>
    public enum VerticalAlignmentOptions : byte
    {
        TopBase,
        TopAscent,
        TopDescent,
        TopCap,
        TopMean,
        BottomBase,
        BottomAscent,
        BottomDescent,
        BottomCap,
        BottomMean,
        MiddleTopAscentToBottomDescent,
    }

    public enum FontWeight : ushort
    {
        //https://learn.microsoft.com/en-us/typography/opentype/spec/os2#usweightclass
        Thin = 100,
        ExtraLight = 200,
        UltraLight = 200,
        Light = 300,
        Normal = 400,
        Regular = 400,
        Medium = 500,
        SemiBold = 600,
        DemiBold = 600,
        Bold = 700,
        ExtraBold = 800,
        UltraBold = 800,
        Black = 900,
        Heavy = 900,
    }

    public enum FontWidth :byte
    {
        //https://learn.microsoft.com/en-us/typography/opentype/spec/os2#uswidthclass
        UltraCondensed = 50,
        ExtraCondensed = 63,//62.5,
        Narrow = 75,
        Condensed = 75,
        SemiCondensed = 88,//87.5,
        Normal = 100,
        SemiExpanded = 113, //112.5,
        Expanded = 125,
        ExtraExpanded = 150,
        UltraExpanded = 200,
    }
    [Flags]
    public enum FontStyles
    {
        [InspectorName("N")] Normal = 0,
        [InspectorName("B")] Bold = 0x1,
        [InspectorName("I")] Italic = 0x2,
        //[InspectorName("_")] Underline = 0x4,
        [InspectorName("low")] LowerCase = 0x8,
        [InspectorName("UPP")] UpperCase = 0x10,
        [InspectorName("Sᴍᴀʟʟ")] SmallCaps = 0x20,
        //[InspectorName("-")] Strikethrough = 0x40,
        [InspectorName("x²")] Superscript = 0x80,
        [InspectorName("x₂")] Subscript = 0x100,
        //[InspectorName("[]")] Highlight = 0x200,
        [InspectorName("½")] Fraction = 0x400,
    }    
}