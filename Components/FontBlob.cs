using TextMeshDOTS.Collections;
using Unity.Collections;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace TextMeshDOTS
{
    public struct FontBlob
    {
        public FixedString128Bytes                name;
        public BlobHashMap<int, GlyphBlob>        glyphs;
        public float atlasSamplingPointSize;
        //public float                              baseLine;
        //public float                              ascentLine;
        //public float                              descentLine;
        //public float                              capLine;
        //public float                              meanLine;
        //public float                              lineHeight;
        //public float                              pointSize;
        //public float                              scale;

        public float atlasWidth;
        public float atlasHeight;

        public float regularStyleSpacing;
        public float regularStyleWeight;
        public float boldStyleSpacing;
        public float boldStyleWeight;
        public byte  italicsStyleSlant;

        //public float subscriptOffset;
        //public float subscriptSize;
        //public float superscriptOffset;
        //public float superscriptSize;

        public float tabWidth;
        public float tabMultiple;

        //public float underlineOffset;

        /// <summary>
        /// Padding that is read from material properties
        /// </summary>
        public float materialPadding;
    }

    public struct GlyphBlob
    {
        public GlyphMetrics glyphMetrics;
        public GlyphRect    glyphRect;
        public float        glyphScale;
    }    
}