using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS
{
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
        public float baseLine; //depends on language and script direction, so risky to do it here.

        public float capHeight;
        public float xHeight;
    }    
}