using HarfBuzz;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.TextCore.Text;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS
{
    #region Baking Components to reference Font on each TextRenderer
    /// <summary> Reference to raw otf and ttf font data</summary>
    [InternalBufferCapacity(1)]
    public struct FontBlobReference : IBufferElementData
    {
        public BlobAssetReference<FontBlob> fontBlob;
        public UnityObjectRef<FontAsset> fontAsset;
    }
    #endregion


    #region Runtime Components NOT stored on TextRenderer but rather on dedicated FontEntity
    /// <summary> HarfBuzz font pointer and static font data extracted by HarfBuzz</summary>
    [InternalBufferCapacity(1)]
    public unsafe struct FontMaterial : IBufferElementData
    {
        public Entity fontEntity;//references font entity. Use to fetch additional components, such as GlyphsInUse
        DynamicFontBlob* m_dynamicFontBlob;
        public Blob blob;
        public Face face;
        public Font font;
        public ref DynamicFontBlob dynamicFontBlob => ref *m_dynamicFontBlob;

        public FontMaterial(Entity fontEntity, Blob blob, Font font, Face face, ref FontBlob fontblob, BlobAssetReference<DynamicFontBlob> dynamicBlobRef)
        {
            this.blob = blob;
            this.face = face;
            this.font = font;
            this.fontEntity = fontEntity;
            m_dynamicFontBlob = (DynamicFontBlob*)dynamicBlobRef.GetUnsafePtr();
        }
    }
    /// <summary>
    /// The glyphs currently in use from this font. Dynamicaly changing! 
    /// Keep in sync with Unity FontAsset! Use conversion to HashSet to accelerate lookup
    /// </summary>
    public struct GlyphsInUse : IBufferElementData
    {
        public uint glyphID;
    }
    public struct MissingGlyphs : IBufferElementData
    {
        public uint glyphID;
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

    public struct DynamicFontBlobReference : IComponentData
    {
        public BlobAssetReference<DynamicFontBlob> blob;
    }
    #endregion
}