using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace TextMeshDOTS
{


    /// <summary>
    /// Purpose of FontBlob is to store reference to desired font (otf, ttf file)
    /// Any kind of dynamic data should be generated during runtime and stored elsewhere
    /// (e.g. which glyphs are currerently used, position of these glyphs in atlas texture,
    /// texture index in case multiple textures are needed) 
    /// </summary>
    public struct FontBlob
    {
        // Unity FontReference.familyName can be HB_OT_NAME_ID.TYPOGRAPHIC_FAMILY or HB_OT_NAME_ID.FONT_FAMILY 
        // Unity FontReference.styleName  = HB_OT_NAME_ID.TYPOGRAPHIC_SUBFAMILY or HB_OT_NAME_ID.FONT_SUBFAMILY
        // https://www.high-logic.com/fontcreator/manual15/fonttype.html
        public FontAssetRef fontAssetRef;
        public FixedString128Bytes fontFamily;
        public FixedString128Bytes fontSubFamily;
        public FixedString128Bytes typographicFamily;       
        public FixedString128Bytes typographicSubfamily;
        public bool useSystemFont;
        public BlobArray<byte> nativeFontFile;

        public override string ToString()
        {
            return $"{fontFamily} {fontSubFamily}";
        }
    }
    /// <summary> dynamic font data from FontAsset (or extracted by HarfBuzz) </summary>
    public struct DynamicFontBlob
    {
        //public FixedString128Bytes familyName;
        //public FixedString128Bytes styleName;
        //public FontAssetRef fontAssetRef;

        #region data from Fontasset which is set by user
        //choose atalas parameter so that number of font glyphs fits! e.g. 60 sampling size and 2048x2048 texture
        public float atlasSamplingPointSize; 
        public float atlasWidth;    
        public float atlasHeight;

        public float materialPadding;//padding read from material properties

        public float regularStyleSpacing;   //default: 0f
        public float boldStyleSpacing;      //default: 7f
        public byte italicsStyleSlant;      //default: 35f
        public float tabWidth;              
        public float tabMultiple;           //default: 10f
        #endregion

        public BlobHashMap<uint, GlyphBlob> glyphs;

        public float ascender; //depends on language and script direction, so risky to do it here. Better move to TextSpan
        public float descender;//depends on language and script direction, so risky to do it here. Better move to TextSpan
        public float baseLine;   //depends on language and script direction, so risky to do it here. Better move to TextSpan

        public float designSize;
        public float subfamilyNameID;
        public float rangeStart;
        public float rangeEnd;
        public float unitsPerEm;
        public float2 scale;

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

        public ScaledDynamicFont(ref DynamicFontBlob dynamicFont, out float xNativeToUnity, out float yNativeToUnity)
        {
            xNativeToUnity = dynamicFont.atlasSamplingPointSize / dynamicFont.scale.x;
            yNativeToUnity = dynamicFont.atlasSamplingPointSize / dynamicFont.scale.y;

            ascender = dynamicFont.ascender * xNativeToUnity;
            descender = dynamicFont.descender * xNativeToUnity;
            baseLine = dynamicFont.baseLine * xNativeToUnity;

            capHeight = dynamicFont.capHeight * xNativeToUnity;
            xHeight = dynamicFont.xHeight * xNativeToUnity;

            subScriptEmXSize = dynamicFont.subScriptEmXSize / dynamicFont.scale.x;
            superScriptEmXSize = dynamicFont.superScriptEmXSize / dynamicFont.scale.x;
            subScriptEmYOffset = dynamicFont.subScriptEmYOffset * yNativeToUnity;
            superScriptEmYOffset = dynamicFont.superScriptEmYOffset * yNativeToUnity;
        }
        public void Update(ref DynamicFontBlob dynamicFont, out float xNativeToUnity, out float yNativeToUnity)
        {
            xNativeToUnity = dynamicFont.atlasSamplingPointSize / dynamicFont.scale.x;
            yNativeToUnity = dynamicFont.atlasSamplingPointSize / dynamicFont.scale.y;

            ascender = dynamicFont.ascender * xNativeToUnity;
            descender = dynamicFont.descender * xNativeToUnity;
            baseLine = dynamicFont.baseLine * xNativeToUnity;

            capHeight = dynamicFont.capHeight * xNativeToUnity;
            xHeight = dynamicFont.xHeight * xNativeToUnity;

            subScriptEmXSize = dynamicFont.subScriptEmXSize / dynamicFont.scale.x;
            superScriptEmXSize = dynamicFont.superScriptEmXSize / dynamicFont.scale.x;
            subScriptEmYOffset = dynamicFont.subScriptEmYOffset * yNativeToUnity;
            superScriptEmYOffset = dynamicFont.superScriptEmYOffset * yNativeToUnity;
        }
    }
}