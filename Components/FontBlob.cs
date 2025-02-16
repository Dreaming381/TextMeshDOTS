using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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
        // Unity FontReference.familyName can be NameID.TYPOGRAPHIC_FAMILY or NameID.FONT_FAMILY 
        // Unity FontReference.styleName  = NameID.TYPOGRAPHIC_SUBFAMILY or NameID.FONT_SUBFAMILY
        // https://www.high-logic.com/fontcreator/manual15/fonttype.html
        public FontAssetRef fontAssetRef;
        public FixedString128Bytes fontFamily;
        public FixedString128Bytes fontSubFamily;
        public FixedString128Bytes typographicFamily;       
        public FixedString128Bytes typographicSubfamily;
        public int samplingPointSizeSDF;
        public int samplingPointSizeBitmap;
        public bool useSystemFont;        
        public FixedString512Bytes fontAssetPath; //To-Do: ensure this points to streamingAssets folder or Application persistent data path

        public override string ToString()
        {
            return $"{fontFamily} {fontSubFamily}";
        }
    }
    /// <summary> dynamic font data extracted by HarfBuzz. Ensure to set scale correctly to the desired sampling point size before  </summary>
    public struct DynamicFontBlob
    {
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

        public float ascender; //depends on language and script direction, so risky to do it here.
        public float descender;//depends on language and script direction, so risky to do it here.
        public float baseLine;   //depends on language and script direction, so risky to do it here.

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
}