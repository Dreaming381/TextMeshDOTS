using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore;

namespace TextMeshDOTS
{
    /// <summary>
    /// Purpose of FontBlob is to store immutable data from original OTF or TTF. 
    /// Any kind of dynamic data should be generated during runtime and stored elsewhere
    /// (e.g. which glyphs are currerently used, position of these glyphs in atlas texture,
    /// texture index in case multiple textures are needed) 
    /// </summary>
    public struct FontBlob
    {
        public FixedString128Bytes name;
        public int fontHash;
        public BlobArray<byte> nativeFontFile;    //needed by Harfbuzz
        
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

        public ScaledDynamicFont(ref DynamicFontBlob dynamicFont, float xNativeToUnity, float yNativeToUnity)
        {
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
        public void Update(ref DynamicFontBlob dynamicFont, float xNativeToUnity, float yNativeToUnity)
        {
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
}