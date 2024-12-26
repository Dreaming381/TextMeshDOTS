using HarfBuzz;
using HarfBuzz.SDF;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS
{
    #region Baking Components
    /// <summary> Reference to raw otf and ttf font data</summary>
    public struct FontBlobReference : IComponentData
    {
        public BlobAssetReference<FontBlob> value;
    }
    #endregion


    #region Native FontAsset Components
    


    /// <summary> Glyphs requested by hb_shape (=they are guarentied to exist in font), </summary>
    [InternalBufferCapacity(0)]
    public struct MissingGlyphs : IBufferElementData
    {
        public uint glyphID;
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
    public struct FreeGlyphRects : IBufferElementData
    {
        public GlyphRect value;
    }    
 
    /// <summary> Add this pointer component upon loading font to enable automatic cleanup once font entity is destroyed </summary>
    public struct DynamicFontAssets : ICleanupComponentData
    {
        public UnityObjectRef<Texture2D> texture;
        public BatchMaterialID fontMaterialID;
        public BlobAssetReference<DynamicFontBlob> blob;
    }
    /// <summary> Add this pointer component upon loading font to enable automatic cleanup once font entity is destroyed </summary>
    public struct NativeFontPointer: ICleanupComponentData
    {
        public SDFOrientation orientation;
        public Blob blob;           //destroy in cleanup system
        public Face face;           //destroy in cleanup system
        public Font font;           //destroy in cleanup system
        public DrawDelegates drawFunctions; //do not destroy this in cleanup system as those functions are needed for loading other fonts
    }
    /// <summary> Contains  relevant data from loading and using font</summary>
    public struct AtlasData : IComponentData
    {
        public int atlasWidth;
        public int atlasHeight;
        public int padding;            //10% of atlas height or width
        public int samplingPointSize;  //size of font (in pixel) in atlas
    }

    public struct FontAssetRef : IEquatable<FontAssetRef>
    {
        //Font selection logic: https://www.high-logic.com/font-editor/fontcreator/tutorials/font-family-settings
        public int familyHash;    //default to typeographic family, and fall-back to family if it does not exist
        public int weight;
        public float width;
        public bool isItalic;
        public float slant;

        public FontAssetRef(FixedString128Bytes fontFamily, FixedString128Bytes typographicFamily, int weight, float width, bool isItalic, float slant)
        {
            this.familyHash = typographicFamily.IsEmpty ? TextHelper.GetHashCodeCaseInSensitive(fontFamily) : TextHelper.GetHashCodeCaseInSensitive(typographicFamily);
            this.weight = weight;
            this.width = width;
            this.isItalic = isItalic;
            this.slant = slant;
        }
        public FontAssetRef(int familyNameHashCode, FontStyles fontStyles)
        {
            this.familyHash = familyNameHashCode;
            this.weight = (fontStyles & FontStyles.Bold) == FontStyles.Bold ? (int)FontWeight.Bold : (int)FontWeight.Normal;
            this.width = (int)FontWidth.Normal;
            this.isItalic = (fontStyles & FontStyles.Italic) == FontStyles.Italic;
            this.slant = 0;
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
            hashCode = hashCode * -1521134295 + weight;
            hashCode = hashCode * -1521134295 + width.GetHashCode();
            hashCode = hashCode * -1521134295 + isItalic.GetHashCode();
            hashCode = hashCode * -1521134295 + slant.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"FamilyHash {familyHash} weigth {weight} width {width} isItalic {isItalic} slant {slant}";
        }
    }
    public struct FontHashMap : IComponentData
    {
        public bool fontsDirty;
        public NativeHashMap<FontAssetRef, Entity> fontEntities;
    }

    public struct FontEntityGlyph
    {
        public Entity entity;
        public uint glyphID;
    }
    public struct FontEntityGlyphComparer : IComparer<FontEntityGlyph>
    {
        public int Compare(FontEntityGlyph a, FontEntityGlyph b)
        {
            if (a.entity == b.entity)
            {
                return 0;                
            }
            else
            {
                if (a.entity.Index > b.entity.Index)
                    return 1;
                else
                    return -1;
            }
        }
    }

    #endregion
}