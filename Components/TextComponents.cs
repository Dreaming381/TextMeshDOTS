using HarfBuzz;
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS
{
    [InternalBufferCapacity(2)]
    public struct MultiFontBlobReferences : IBufferElementData
    {
        public BlobAssetReference<FontBlob> blob;
    }

    /// <summary>
    /// A reference to the font blob asset used for text rendering.
    /// If you choose to change this at runtime, you must also change the material designed to work with the font.
    /// Usage: Typically don't touch, but can be read-write if you know what you are doing.
    /// </summary>
    public struct FontBlobReference : IComponentData
    {
        public BlobAssetReference<FontBlob> blob;
    }
    public struct NativeFont : IComponentData
    {
        public bool isCreated;
        public Face nativeFace;
        public Font nativeFont;
        public Blob nativeBlob;

        public float designSize;
        public float subfamilyNameID;
        public float rangeStart;
        public float rangeEnd;
        public float unitsPerEm;
        public float xScale;
        public float yScale;
        public float baseline;

        public float capHeight;
        public float xHeight;
         
        public float subScriptEmXSize;
        public float subScriptEmYSize;
        public float subScriptEmXOffset;
        public float subScriptEmYOffset;

        public float superScriptEmXSize;
        public float superScriptEmYSize;
        public float superScriptEmXOffset;
        public float superScriptEmYOffset;

        public FontExtents GetFontExtents(Direction direction)
        {
            nativeFont.GetFontExtentsForDirection(direction, out FontExtents fontExtents);
            return fontExtents;
        }

        public NativeFont(string path)
        {
            isCreated = true;
            nativeBlob = new Blob(path);
            nativeFace = new Face(nativeBlob.ptr, 0);
            nativeFont = new Font(nativeFace.ptr);

            nativeFont.MakeImmutable();

            unitsPerEm = nativeFace.UnitsPerEM;
            //nativeFont.GetBaseline(Direction.LeftToRight, HB.hb_language_from_string("en"));
            baseline = 0; 

            nativeFace.GetSizeParams(out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end);
            nativeFont.GetScale(out int x_scale, out int y_scale);

            nativeFont.GetMetrics(MetricTag.CapHeight, out int capHeight);
            nativeFont.GetMetrics(MetricTag.XHeight, out int xHeight);

            nativeFont.GetMetrics(MetricTag.SubScriptEmXSize, out int subScriptEmXSize);
            nativeFont.GetMetrics(MetricTag.SubScriptEmYSize, out int subScriptEmYSize);
            nativeFont.GetMetrics(MetricTag.SubScriptEmXOffset, out int subScriptEmXOffset);
            nativeFont.GetMetrics(MetricTag.SubScriptEmYOffset, out int subScriptEmYOffset);

            nativeFont.GetMetrics(MetricTag.SuperScriptEmXSize, out int superScriptEmXSize);
            nativeFont.GetMetrics(MetricTag.SuperScriptEmYSize, out int superScriptEmYSize);
            nativeFont.GetMetrics(MetricTag.SuperScriptEmXOffset, out int superScriptEmXOffset);            
            nativeFont.GetMetrics(MetricTag.SuperScriptEmYOffset, out int superScriptEmYOffset);

            this.designSize = design_size;
            this.subfamilyNameID = subfamily_name_id;
            this.rangeStart = range_start;
            this.rangeEnd = range_end;

            this.xScale = x_scale;
            this.yScale = y_scale;

            this.capHeight = capHeight;
            this.xHeight = xHeight;

            this.subScriptEmXSize = subScriptEmXSize;
            this.subScriptEmYSize = subScriptEmYSize;
            this.subScriptEmXOffset = subScriptEmXOffset;
            this.subScriptEmYOffset = subScriptEmYOffset;

            this.superScriptEmXSize = superScriptEmXSize;
            this.superScriptEmYSize = superScriptEmYSize;
            this.superScriptEmXOffset = superScriptEmXOffset;
            this.superScriptEmYOffset = superScriptEmYOffset;
        }
    }
    public class FontAssetReference : IComponentData
    {
        public FontAsset value;
    }

    /// <summary>
    /// The base settings of the text before any rich text tags or animations are applied.
    /// Usage: ReadWrite
    /// </summary>
    public struct TextBaseConfiguration : IComponentData
    {
        public float fontSize;        
        public Color32 color;
        public FontStyles fontStyle;

        public float maxLineWidth;
        public float wordSpacing;
        public float lineSpacing;
        public float paragraphSpacing;
        public HorizontalAlignmentOptions lineJustification;
        public VerticalAlignmentOptions verticalAlignment;
        public bool isOrthographic;
    }

    [InternalBufferCapacity(0)]
    public struct TextSpan : IBufferElementData
    {
        //public IntPtr fontAsset;    //to access native Font  
        public int fontMaterialIndex; //to access Font Blob
        public int startIndex;
        public int length;        

        public int fontSize;
        public Color32 color;
        public FontStyles fontStyle;
        public TextFontWeight fontWeight;
        public HorizontalAlignmentOptions lineJustification;

        public float monoSpacing;
        public float cSpacing;
        public float fxScale;
        public float fxRotationAngleCCW;
        public short italicAngle;        

        public override string ToString()
        {
            //return string.Format("{0}: {1}\n", "color", color) + string.Format("{0}: {1}\n", "fontStyle", fontStyle) + string.Format("{0}: {1}\n", "fontWeight", fontWeight) + string.Format("{0}: {1}\n", "fontSize", fontSize) + string.Format("{0}: {1}", "fontAsset", fontAsset) + string.Format("{0}: {1}\n", "startIndex", startIndex) + string.Format("{0}: {1}", "length", length);
            return string.Format("{0}: {1}\n", "color", color) + string.Format("{0}: {1}\n", "fontStyle", fontStyle) + string.Format("{0}: {1}\n", "fontWeight", fontWeight) + string.Format("{0}: {1}\n", "fontSize", fontSize) + string.Format("{0}: {1}", "fontAsset", "startIndex", startIndex) + string.Format("{0}: {1}", "length", length);
        }
    }

    /// <summary>
    /// The raw byte element as part of the text string.
    /// Prefer to use TextRendererAspect or cast to CalliString instead.
    /// Usage: ReadWrite, but using the abstraction tools.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CalliByteRaw : IBufferElementData
    {
        public byte element;
    }
    [InternalBufferCapacity(0)]
    public struct CalliByte : IBufferElementData
    {
        public byte element;
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

    public enum FontWeight
    {
        Thin,
        ExtraLight,
        Light,
        Regular,
        Medium,
        SemiBold,
        Bold,
        Heavy,
        Black
    };
}