using HarfBuzz;
using System;
using System.Collections.Generic;
using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;
using static TreeEditor.TextureAtlas;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS
{
    #region Baking Components to reference Font on each TextRenderer
    /// <summary> Reference to raw otf and ttf font data</summary>
    [InternalBufferCapacity(1)]
    public struct FontBlobReference : IBufferElementData
    {
        public BlobAssetReference<FontBlob> blob;
    }
    /// <summary> Reference to linked Unity FontAsset</summary>
    public class FontAssetReferences : IComponentData
    {
        public List<FontAsset> value;
    }
    public class FontAssetReference : IComponentData
    {
        public FontAsset value;
    }
    #endregion


    #region Runtime Components NOT stored on TextRenderer but rather on dedicated FontEntity
    [InternalBufferCapacity(1)]
    public unsafe struct FontMaterial : IBufferElementData
    {
        public Entity fontEntity;//references font entity. Use to fetch additional components, such as GlyphsInUse
        FontBlob* m_fontBlobPtr;
        DynamicFontBlob* m_dynamicFontBlob;
        public Font hbFont;

        public ref FontBlob fontBlob => ref *m_fontBlobPtr;
        public ref DynamicFontBlob dynamicFontBlob => ref *m_dynamicFontBlob;

        public FontMaterial(Entity fontEntity, BlobAssetReference<FontBlob> blobRef, BlobAssetReference<DynamicFontBlob> dynamicBlobRef, HBFontAssetReference hBFontAsset)
        {
            this.fontEntity = fontEntity;
            m_fontBlobPtr = (FontBlob*)blobRef.GetUnsafePtr();
            m_dynamicFontBlob = (DynamicFontBlob*)dynamicBlobRef.GetUnsafePtr();
            hbFont = hBFontAsset.font;
            //hbFont = (Font*)&hBFontAsset.font;
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


    /// <summary> HarfBuzz font pointer and static font data extracted by HarfBuzz</summary>
    public struct HBFontAssetReference : IComponentData, IEquatable<HBFontAssetReference>
    {
        public FixedString128Bytes familyName;
        public FixedString128Bytes styleName; //this is a composite of TextFontWeight and (regular/italic)
        public FontAssetRef fontAssetRef;

        public Face face;
        public Font font;
        public Blob blob;

        //unsafe public HBFontAssetReference(ref FontBlob fontblob)
        //{            
        //    fontHash = fontblob.fontAssetRef;
        //    blob = new Blob(fontblob.nativeFontFile.GetUnsafePtr(), (uint)fontblob.nativeFontFile.Length, MemoryMode.Readonly);
        //    face = new Face(blob.ptr, 0);
        //    font = new Font(face.ptr);
        //    font.MakeImmutable();
        //    //Debug.Log($"HBFontAssetReference {fontHash} {fontblob.name} {fontblob.fontHash}");

        //    //Debug.Log($"Loaded? {path} Blob:{nativeBlob.ptr != IntPtr.Zero} (Length:{nativeBlob.Length}) Face:{nativeFace.ptr != IntPtr.Zero} Font:{nativeFont.ptr != IntPtr.Zero}");
        //    //Debug.Log($"Loaded? {fontblob.name} Blob:{nativeBlob.ptr != IntPtr.Zero} (Length:{nativeBlob.Length}) Face:{nativeFace.ptr != IntPtr.Zero} Font:{nativeFont.ptr != IntPtr.Zero}");
        //}
        unsafe public HBFontAssetReference(ref FontBlob fontblob, string fileName)
        {
            familyName = fontblob.familyName;
            styleName = fontblob.styleName;
            fontAssetRef = fontblob.fontAssetRef;

            blob = new Blob(fileName);
            face = new Face(blob.ptr, 0);
            font = new Font(face.ptr);

            //in order to get the same scaled GlyphMetrics as Unity, we could set the scale to atlasSamplingPointSize,
            //but this would eliminate all units of precision as Harfbuzz works internaly with int
            //so better to do this correction during glyph generation
            //font.SetScale((int)fontblob.atlasSamplingPointSize, (int)fontblob.atlasSamplingPointSize); 

            font.MakeImmutable();
            //Debug.Log($"HBFontAssetReference {fontHash} {fontblob.name} {fontblob.fontHash}");

            //Debug.Log($"Loaded? {path} Blob:{nativeBlob.ptr != IntPtr.Zero} (Length:{nativeBlob.Length}) Face:{nativeFace.ptr != IntPtr.Zero} Font:{nativeFont.ptr != IntPtr.Zero}");
            //Debug.Log($"Loaded? {fontblob.name} Blob:{nativeBlob.ptr != IntPtr.Zero} (Length:{nativeBlob.Length}) Face:{nativeFace.ptr != IntPtr.Zero} Font:{nativeFont.ptr != IntPtr.Zero}");
        }

        public override bool Equals(object obj) => obj is HBFontAssetReference other && Equals(other);
        public bool Equals(HBFontAssetReference other)
        {
            return fontAssetRef == other.fontAssetRef;
        }

        public static bool operator ==(HBFontAssetReference e1, HBFontAssetReference e2)
        {
            return e1.fontAssetRef == e2.fontAssetRef;
        }
        public static bool operator !=(HBFontAssetReference e1, HBFontAssetReference e2)
        {
            return e1.fontAssetRef != e2.fontAssetRef;
        }
        public override int GetHashCode()
        {
            return fontAssetRef.GetHashCode();
        }

    }
    #endregion

    [InternalBufferCapacity(2)]
    public struct MultiFontBlobReferences : IBufferElementData
    {
        public BlobAssetReference<FontBlob> blob;
        public Entity runtimeFontDataEntity;
    }
}