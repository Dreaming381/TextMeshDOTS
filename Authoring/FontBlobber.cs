using HarfBuzz;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Font = HarfBuzz.Font;


namespace TextMeshDOTS.Authoring
{
    public static class FontBlobber
    {
        public static BlobAssetReference<FontBlob> BakeFontBlob(UnityEngine.Font fontItem, bool useSystemFont)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref FontBlob fontBlobRoot = ref builder.ConstructRoot<FontBlob>();

            byte[] fontBytes;

            var filePath = AssetDatabase.GetAssetPath(fontItem);            
            fontBytes = File.ReadAllBytes(filePath);

            if (useSystemFont)
            {
                fontBlobRoot.useSystemFont = useSystemFont;
                BlobBuilderArray<byte> nativeFontFileBytes = builder.Allocate(ref fontBlobRoot.nativeFontFile, 0);
            }
            else
            {
                fontBlobRoot.useSystemFont = useSystemFont;
                BlobBuilderArray<byte> nativeFontFileBytes = builder.Allocate(ref fontBlobRoot.nativeFontFile, fontBytes.Length);
                for (int i = 0, length = fontBytes.Length; i < length; i++)
                    nativeFontFileBytes[i] = fontBytes[i];                
            }

            Blob blob;
            unsafe
            {
                fixed (byte* bytes = fontBytes)
                {
                    blob = new Blob(bytes, (uint)fontBytes.Length, MemoryMode.Readonly);
                }
            }

            var face = new Face(blob.ptr, 0);
            var font = new Font(face.ptr);

            //fetch name of fontFamily and subFamily, generate hash code from that used to lookup this font
            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));

            var initialCapacity = 125u; //FixedString128Bytes.Capacity
            fontBlobRoot.fontFamily = new FixedString128Bytes();
            uint textSize = initialCapacity;
            face.GetFaceInfo(HB_OT_NAME_ID.FONT_FAMILY, language, ref textSize, ref fontBlobRoot.fontFamily);
            fontBlobRoot.fontFamily.Length = (int)textSize;

            fontBlobRoot.fontSubFamily = new FixedString128Bytes();
            textSize = initialCapacity;
            face.GetFaceInfo(HB_OT_NAME_ID.FONT_SUBFAMILY, language, ref textSize, ref fontBlobRoot.fontSubFamily);
            fontBlobRoot.fontSubFamily.Length = (int)textSize;

            fontBlobRoot.typographicFamily = new FixedString128Bytes();
            textSize = initialCapacity;
            face.GetFaceInfo(HB_OT_NAME_ID.TYPOGRAPHIC_FAMILY, language, ref textSize, ref fontBlobRoot.typographicFamily);
            fontBlobRoot.typographicFamily.Length = (int)textSize;

            fontBlobRoot.typographicSubfamily = new FixedString128Bytes();
            textSize = initialCapacity;
            face.GetFaceInfo(HB_OT_NAME_ID.TYPOGRAPHIC_SUBFAMILY, language, ref textSize, ref fontBlobRoot.typographicSubfamily);
            fontBlobRoot.typographicSubfamily.Length = (int)textSize;

            var weight = font.GetStyleTag(StyleTag.Weight);
            var width = font.GetStyleTag(StyleTag.Width);
            var italic = (byte)font.GetStyleTag(StyleTag.Italic);
            bool isItalic;
            switch (italic)
            {
                case 1:
                    isItalic = true;
                    break;
                case 0:
                default:
                    isItalic = false;
                    break;
            }
            var slant = font.GetStyleTag(StyleTag.SlantAngle);

            fontBlobRoot.fontAssetRef = new FontAssetRef(fontBlobRoot.fontFamily, fontBlobRoot.typographicFamily, (int)weight, width, isItalic, slant);

            //var result = new FixedString128Bytes();
            //var values = Enum.GetValues(typeof(HB_OT_NAME_ID));
            //foreach (HB_OT_NAME_ID value in values)
            //{
            //    textSize = (uint)result.Capacity;
            //    face.GetFaceInfo(value, language, ref textSize, ref result);
            //    result.Length = (int)textSize;
            //    Debug.Log($"{value}: {result}");
            //    result.Clear();
            //}

            //Debug.Log($"Weight: {weight}");
            //Debug.Log($"Width: {width}");
            //Debug.Log($"Italic?: {isItalic}");

            font.Dispose();
            face.Dispose();
            blob.Dispose();

            var result = builder.CreateBlobAssetReference<FontBlob>(Allocator.Persistent);
            builder.Dispose();
            fontBlobRoot = result.Value;
            return result;
        }
    }
}