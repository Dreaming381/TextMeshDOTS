using HarfBuzz;
using System;
using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS
{
    /// <summary>
    /// Purpose of FontBlob is to store immutable data from original OTF or TTF. 
    /// Any kind of dynamic data should be generated during runtime and stored elsewhere
    /// (e.g. which glyphs are currerently used, position of these glyphs in atlas texture,
    /// texture index in case multiple textures are needed) 
    /// </summary>
    public struct FontBlob : IEquatable<FontBlob>
    {
        public FixedString128Bytes familyName;
        public FixedString128Bytes styleName;
        public FontAssetRef fontAssetRef;
        //public BlobArray<byte> nativeFontFile;    //needed by Harfbuzz

        public float atlasSamplingPointSize;
        public float atlasWidth;
        public float atlasHeight;
        /// <summary> Padding that is read from material properties </summary>
        public float materialPadding;

        public float regularStyleSpacing;
        public float boldStyleSpacing;
        public byte  italicsStyleSlant;
        public float tabWidth;
        public float tabMultiple;

        public override bool Equals(object obj) => obj is FontBlob other && Equals(other);
        public bool Equals(FontBlob other)
        {
            return fontAssetRef == other.fontAssetRef;
        }

        public static bool operator ==(FontBlob e1, FontBlob e2)
        {
            return e1.fontAssetRef == e2.fontAssetRef;
        }
        public static bool operator !=(FontBlob e1, FontBlob e2)
        {
            return e1.fontAssetRef != e2.fontAssetRef;
        }
        public override int GetHashCode()
        {
            return fontAssetRef.GetHashCode();
        }
    }
    /// <summary> dynamic font data from FontAsset (or extracted by HarfBuzz) </summary>
    public struct DynamicFontBlob
    {
        public BlobHashMap<uint, GlyphBlob> glyphs;

        public float ascender; //depends on language and script direction, so risky to do it here. Better move to TextSpan
        public float descender;//depends on language and script direction, so risky to do it here. Better move to TextSpan
        public float baseLine;   //depends on language and script direction, so risky to do it here. Better move to TextSpan

        public float designSize;
        public float subfamilyNameID;
        public float rangeStart;
        public float rangeEnd;
        public float unitsPerEm;
        public float xScale;
        public float yScale;

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
    }

    /// <summary> copy of DynamicFontBlob used during Glyphgeneration to scale some factor </summary>
    public struct ScaledDynamicFont
    {
        public float ascender;
        public float descender;
        public float baseLine;

        //public float designSize;
        //public float subfamilyNameID;
        //public float rangeStart;
        //public float rangeEnd;
        //public float unitsPerEm;
        //public float xScale;
        //public float yScale;

        public float capHeight;
        public float xHeight;

        public float subScriptEmXSize;
        //public float subScriptEmYSize;
        //public float subScriptEmXOffset;
        public float subScriptEmYOffset;

        public float superScriptEmXSize;
        //public float superScriptEmYSize;
        //public float superScriptEmXOffset;
        public float superScriptEmYOffset;

        public ScaledDynamicFont(ref DynamicFontBlob dynamicFont, ref FontBlob fontBlob, out float xNativeToUnity, out float yNativeToUnity)
        {
            xNativeToUnity = fontBlob.atlasSamplingPointSize / dynamicFont.yScale;
            yNativeToUnity = fontBlob.atlasSamplingPointSize / dynamicFont.xScale;

            ascender = dynamicFont.ascender * xNativeToUnity;
            descender = dynamicFont.descender * xNativeToUnity;
            baseLine = dynamicFont.baseLine * xNativeToUnity;

            capHeight = dynamicFont.capHeight * xNativeToUnity;
            xHeight = dynamicFont.xHeight * xNativeToUnity;

            subScriptEmXSize = dynamicFont.subScriptEmXSize / dynamicFont.xScale;
            superScriptEmXSize = dynamicFont.superScriptEmXSize / dynamicFont.xScale;
            subScriptEmYOffset = dynamicFont.subScriptEmYOffset * yNativeToUnity;
            superScriptEmYOffset = dynamicFont.superScriptEmYOffset * yNativeToUnity;
        }
        public void Update(ref DynamicFontBlob dynamicFont, ref FontBlob fontBlob, out float xNativeToUnity, out float yNativeToUnity)
        {
            xNativeToUnity = fontBlob.atlasSamplingPointSize / dynamicFont.yScale;
            yNativeToUnity = fontBlob.atlasSamplingPointSize / dynamicFont.xScale;

            ascender = dynamicFont.ascender * xNativeToUnity;
            descender = dynamicFont.descender * xNativeToUnity;
            baseLine = dynamicFont.baseLine * xNativeToUnity;

            capHeight = dynamicFont.capHeight * xNativeToUnity;
            xHeight = dynamicFont.xHeight * xNativeToUnity;

            subScriptEmXSize = dynamicFont.subScriptEmXSize / dynamicFont.xScale;
            superScriptEmXSize = dynamicFont.superScriptEmXSize / dynamicFont.xScale;
            subScriptEmYOffset = dynamicFont.subScriptEmYOffset * yNativeToUnity;
            superScriptEmYOffset = dynamicFont.superScriptEmYOffset * yNativeToUnity;
        }
    }
    public struct GlyphBlob
    {
        public uint glyphID;
        public GlyphMetrics glyphMetrics;   //source: UnityFontAsset or Harfbuzz (GlyphExtends)
        public GlyphRect glyphRect;         //source: UnityFontAsset 
        public float glyphScale;            //source: UnityFontAsset. Review why this is needed
    }
    public struct FontAssetRef : IEquatable<FontAssetRef>
    {
        public int familyNameHash;
        public TextFontWeight textFontWeight;
        public bool isItalic;

        public FontAssetRef(int familyNameHash, TextFontWeight textFontWeight, bool isItalic)
        {
            this.familyNameHash = familyNameHash;
            this.textFontWeight = textFontWeight;
            this.isItalic = isItalic;
        }
        public override bool Equals(object obj) => obj is FontAssetRef other && Equals(other);

        public bool Equals(FontAssetRef other)
        {
            return familyNameHash == other.familyNameHash && textFontWeight == other.textFontWeight && isItalic == other.isItalic;
        }

        public static bool operator ==(FontAssetRef e1, FontAssetRef e2)
        {
            return e1.familyNameHash == e2.familyNameHash && e1.textFontWeight == e2.textFontWeight && e1.isItalic == e2.isItalic;
        }
        public static bool operator !=(FontAssetRef e1, FontAssetRef e2)
        {
            return e1.familyNameHash != e2.familyNameHash || e1.textFontWeight != e2.textFontWeight || e1.isItalic != e2.isItalic;
        }
        public override int GetHashCode()
        {
            //return HashCode.Combine(mapID, scamin, TextureName);
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + familyNameHash;
            hashCode = hashCode * -1521134295 + textFontWeight.GetHashCode();
            hashCode = hashCode * -1521134295 + isItalic.GetHashCode();
            return hashCode;
        }
        public override string ToString()
        {
            return $"FamilyHash {familyNameHash} textFontWeight {textFontWeight} isItalic {isItalic}";
        }
    }
}