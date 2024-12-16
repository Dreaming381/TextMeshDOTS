using HarfBuzz;
using HarfBuzz.SDF;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS
{
    #region Baking Components
    /// <summary> Reference to raw otf and ttf font data</summary>
    [InternalBufferCapacity(1)]
    public struct FontBlobReference : IBufferElementData
    {
        public BlobAssetReference<FontBlob> fontBlob;
        public UnityObjectRef<FontAsset> fontAsset;
    }
    #endregion


    #region Native FontAsset Components
    
    /// <summary> Contains  relevant data from loading and using font</summary>
    public struct HBFontAssetRef : IComponentData
    {
        public FixedString128Bytes family;
        public FixedString128Bytes subFamily;
        public FontAssetRef fontAssetRef;
        public int atlasWidth;
        public int atlasHeight;
        public int padding;            //10% of atlas height or width
        public int samplingPointSize;  //size of font (in pixel) in atlas
    }

    /// <summary> Glyphs requested by hb_shape (=they are guarentied to exist in font), </summary>
    [InternalBufferCapacity(0)]
    public struct HBMissingGlyphs : IBufferElementData
    {
        public uint glyphID;
    }
    
    /// <summary> ID's of glyphs currently placed in the texture atlas. Keep order aligend with UsedGlyphRects </summary>
    [InternalBufferCapacity(0)]
    public struct HBGlyphsInUse : IBufferElementData
    {
        public uint glyphID;
    }

    /// <summary> GlyphsRects currently used in the texture atlas. Keep order aligend with GlyphsInUse </summary>
    [InternalBufferCapacity(0)]
    public struct HBUsedGlyphRects : IBufferElementData
    {
        public GlyphRect value;
    }
    /// <summary> Free GlyphsRects of texture atlas </summary>
    public struct HBFreeGlyphRects : IBufferElementData
    {
        public GlyphRect value;
    }    
 
    /// <summary> Add this pointer component upon loading font to enable automatic cleanup once font entity is destroyed </summary>
    public struct FontTextureReference : ICleanupComponentData
    {
        public UnityObjectRef<Texture2D> texture;
        public UnityObjectRef<Material> material;        
        public BlobAssetReference<DynamicFontBlob> blob;
    }
    /// <summary> Add this pointer component upon loading font to enable automatic cleanup once font entity is destroyed </summary>
    public struct HBFontPointer: ICleanupComponentData
    {
        public FontAssetRef fontAssetRef;
        public SDFOrientation orientation;
        public Blob blob;           //destroy in cleanup system
        public Face face;           //destroy in cleanup system
        public Font font;           //destroy in cleanup system
        public IntPtr hbDrawFuncts; //do not destroy this in cleanup system as those functions are needed for loading other fonts
    }


    
    //Attach this component to all TextRenderer to enable use of fonts referenced in this buffer
    public struct FontEntity : IBufferElementData
    {
        public Entity value;
    }
    public  struct CreatedFromFontAsset : IComponentData 
    {
        public UnityObjectRef<FontAsset> fontAsset;
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