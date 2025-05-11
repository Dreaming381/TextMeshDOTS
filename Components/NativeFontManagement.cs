using TextMeshDOTS.HarfBuzz;
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;

namespace TextMeshDOTS
{
    #region Baking Components
    /// <summary> Reference to raw otf and ttf font data</summary>
    public struct FontBlobReference : IComponentData
    {
        public FontAssetRef value;
        //public BlobAssetReference<FontBlob> value; //FontBlob is redundant to FontRequest struct
    }
    #endregion

    /// <summary> Add this pointer component upon loading font to enable automatic cleanup once font entity is destroyed </summary>
    public struct DrawAndPaintFunctions: IComponentData
    {
        public DrawDelegates drawFunctions;
        public PaintDelegates paintFunctions;
    }

    #region Native FontAsset Components    

    /// <summary> Glyphs requested by hb_shape (=they are guaranteed to exist in font), </summary>
    [InternalBufferCapacity(0)]
    public struct MissingGlyphs : IBufferElementData
    {
        internal GlyphTable.Key key;
    }
    
    /// <summary> ID's of glyphs currently placed in the texture atlas. Keep order aligend with UsedGlyphRects </summary>
    [InternalBufferCapacity(0)]
    public struct UsedGlyphs : IBufferElementData
    {
        public uint glyphID;
    }

    /// <summary> GlyphsRects currently used in the texture atlas. Keep order aligend with GlyphsInUse </summary>
    [InternalBufferCapacity(0)]
    public struct UsedGlyphRects : IBufferElementData
    {
        public GlyphRect value;
    }
    /// <summary> Free GlyphsRects of texture atlas </summary>
    [InternalBufferCapacity(0)]
    public struct FreeGlyphRects : IBufferElementData
    {
        public GlyphRect value;
    }    
 
    /// <summary> Add this pointer component upon loading font to enable automatic cleanup once font entity is destroyed </summary>
    public struct DynamicFontAsset : ICleanupComponentData
    {
        public UnityObjectRef<Material> debugMaterial;
        public TextureType textureType;
        public UnityObjectRef<Texture2D> texture;        
        public BatchMaterialID fontMaterialID;
        public BatchMeshID backendMeshID;
        public BlobAssetReference<DynamicFontBlob> blob;
    }

    /// <summary> Contains  relevant data from loading and using font</summary>
    public struct AtlasData : IComponentData
    {
        public int atlasWidth;
        public int atlasHeight;
        public int padding;            //10% of atlas height or width
        public int samplingPointSize;  //size of font (in pixel) in atlas
    }

    //while it is tempting to just copy FontBlobReference to font entities, it will not work:
    //font entities need to be stable during incremental baking where additional data
    //is merged into the entiy world. Unfortunately, this can invalidate all blob pointer
    //from a previous baking run. Adding instead the FontAssetRef and FontAssetMetadata signifiantly
    //reduces chunk capacity for font entities, but should be "rock solid"
    public struct FontAssetMetadata : IComponentData
    {
        public FixedString128Bytes family;
        public FixedString128Bytes subfamily;
        public int faceIndex; //temporary link from FontEntity to FontTable
    }

    /// <summary>
    /// FontAssetRef is THE link between any fonts request and font entities, and consists of a hash representing the 
    /// font family, and variation axis used during typesetting such as weight ("normal", "bold", semibold"), 
    /// width ("condensed", normal"), and italic. Slant is ignored in such font matching (see GetHashcode) 
    /// because slant value cannot be "guessed" and requested by user during typesetting 
    /// </summary>
    [Serializable]
    public struct FontAssetRef : IEquatable<FontAssetRef>, IComponentData
    {
        //Font selection logic: https://www.high-logic.com/font-editor/fontcreator/tutorials/font-family-settings
        public int familyHash;    //default to typeographic family, and fall-back to family if it does not exist
        public FontWeight weight;
        public float width;
        public bool isItalic;
        public float slant;

        public FontAssetRef(FixedString128Bytes fontFamily, FixedString128Bytes typographicFamily, FontWeight fontWeight, float width, bool isItalic, float slant)
        {
            this.familyHash = typographicFamily.IsEmpty ? TextHelper.GetHashCodeCaseInSensitive(fontFamily) : TextHelper.GetHashCodeCaseInSensitive(typographicFamily);
            this.weight = fontWeight;
            this.width = width;
            this.isItalic = isItalic;
            this.slant = slant;
        }
        public FontAssetRef(int familyNameHashCode, FontWeight fontWeight, float fontWidth, FontStyles fontStyles)
        {
            familyHash = familyNameHashCode;
            weight = fontWeight;
            width = fontWidth;
            isItalic = (fontStyles & FontStyles.Italic) == FontStyles.Italic;
            slant = 0;
        }
        public FontAssetRef(int familyNameHashCode, FontWeight fontWeight, FontWidth fontWidth, FontStyles fontStyles)
        {

            familyHash = familyNameHashCode;
            weight = fontWeight;
            width = (float)fontWidth;
            isItalic = (fontStyles & FontStyles.Italic) == FontStyles.Italic;
            slant = 0;
            //adjust according to https://learn.microsoft.com/en-us/typography/opentype/spec/os2#uswidthclass
            switch (fontWidth)
            {
                case FontWidth.ExtraCondensed:
                    width = 62.5f;
                    break;
                case FontWidth.SemiCondensed:
                    width = 87.5f;
                    break;
                case FontWidth.SemiExpanded:
                    width = 112.5f;
                    break;
            }
        }

        public override bool Equals(object obj) => obj is FontAssetRef other && Equals(other);

        public bool Equals(FontAssetRef other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        public static bool operator ==(FontAssetRef e1, FontAssetRef e2)
        {
            return e1.GetHashCode() == e2.GetHashCode();
        }
        public static bool operator !=(FontAssetRef e1, FontAssetRef e2)
        {
            return e1.GetHashCode() != e2.GetHashCode();
        }
        
        public override int GetHashCode()
        {
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + familyHash;
            hashCode = hashCode * -1521134295 + (int)weight;
            hashCode = hashCode * -1521134295 + width.GetHashCode();
            hashCode = hashCode * -1521134295 + isItalic.GetHashCode();
            //fonts are search at runtime via FontAssetRef match. As slant angle cannot be guessed, do not inlcude this in hash
            //hashCode = hashCode * -1521134295 + slant.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"FamilyHash {familyHash} weigth {weight} width {width} isItalic {isItalic}";
        }
    }
    public struct FontState : IComponentData { };
    public struct FontsDirtyTag : IComponentData { }

    public enum TextureType: byte
    {
        ARGB = 0,
        SDF = 1,
    }
    #endregion
}