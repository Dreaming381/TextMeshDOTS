using TextMeshDOTS.HarfBuzz;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Font = TextMeshDOTS.HarfBuzz.Font;
using UnityEditor;
using UnityEngine;

namespace TextMeshDOTS.Authoring
{
    public static class FontBlobber
    {
        public static BlobAssetReference<FontBlob> BakeFontBlob(Object fontItem, bool useSystemFont, int samplingPointSizeSDF, int samplingPointSizeBitmap)
        {
            string fontPath = default;
#if UNITY_EDITOR
            fontPath = AssetDatabase.GetAssetPath(fontItem);
#endif
            var builder = new BlobBuilder(Allocator.Temp);
            ref FontBlob fontBlobRoot = ref builder.ConstructRoot<FontBlob>();
            fontBlobRoot.samplingPointSizeSDF = samplingPointSizeSDF;
            fontBlobRoot.samplingPointSizeBitmap = samplingPointSizeBitmap;
            fontBlobRoot.useSystemFont = useSystemFont;
            fontBlobRoot.fontAssetPath = useSystemFont ? string.Empty : fontPath.Substring(fontPath.IndexOf("StreamingAssets") + 16);
            var fontBytes = File.ReadAllBytes(fontPath);

            Blob blob;
            unsafe
            {
                fixed (byte* bytes = fontBytes)
                {
                    blob = new Blob(bytes, (uint)fontBytes.Length, MemoryMode.READONLY);
                }
            }

            var face = new Face(blob.ptr, 0);
            var font = new Font(face.ptr);

            //fetch name of fontFamily and subFamily, generate hash code from that used to lookup this font
            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));

            var initialCapacity = 125u; //FixedString128Bytes.Capacity
            fontBlobRoot.fontFamily = new FixedString128Bytes();
            uint textSize = initialCapacity;
            face.GetFaceInfo(NameID.FONT_FAMILY, language, ref textSize, ref fontBlobRoot.fontFamily);
            fontBlobRoot.fontFamily.Length = (int)textSize;

            fontBlobRoot.fontSubFamily = new FixedString128Bytes();
            textSize = initialCapacity;
            face.GetFaceInfo(NameID.FONT_SUBFAMILY, language, ref textSize, ref fontBlobRoot.fontSubFamily);
            fontBlobRoot.fontSubFamily.Length = (int)textSize;

            fontBlobRoot.typographicFamily = new FixedString128Bytes();
            textSize = initialCapacity;
            face.GetFaceInfo(NameID.TYPOGRAPHIC_FAMILY, language, ref textSize, ref fontBlobRoot.typographicFamily);
            fontBlobRoot.typographicFamily.Length = (int)textSize;

            fontBlobRoot.typographicSubfamily = new FixedString128Bytes();
            textSize = initialCapacity;
            face.GetFaceInfo(NameID.TYPOGRAPHIC_SUBFAMILY, language, ref textSize, ref fontBlobRoot.typographicSubfamily);
            fontBlobRoot.typographicSubfamily.Length = (int)textSize;            

            var weight = font.GetStyleTag(StyleTag.WEIGHT);
            var width = font.GetStyleTag(StyleTag.WIDTH);
            var italic = (byte)font.GetStyleTag(StyleTag.ITALIC);
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
            var slant = font.GetStyleTag(StyleTag.SLANT_ANGLE); 

            fontBlobRoot.fontAssetRef = new FontAssetRef(fontBlobRoot.fontFamily, fontBlobRoot.typographicFamily, (int)weight, width, isItalic, slant);

            //var result = new FixedString128Bytes();
            //var values = Enum.GetValues(typeof(NameId));
            //foreach (NameId value in values)
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
            return result;
        }

        public static BlobAssetReference<FontBlob> GetRuntimeFontBlob(
            FixedString128Bytes fontAssetPath,
            FixedString128Bytes fontFamily,
            FixedString128Bytes fontSubFamily,
            FixedString128Bytes typographicFamily,
            FixedString128Bytes typographicSubfamily,
            int weight,
            int width,
            bool isItalic,
            int slant,
            bool useSystemFont, 
            int samplingPointSizeSDF, 
            int samplingPointSizeBitmap)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref FontBlob fontBlobRoot = ref builder.ConstructRoot<FontBlob>();
            fontBlobRoot.samplingPointSizeSDF = samplingPointSizeSDF;
            fontBlobRoot.samplingPointSizeBitmap = samplingPointSizeBitmap;
            fontBlobRoot.useSystemFont = useSystemFont;
            fontBlobRoot.fontAssetPath = fontAssetPath;
            fontBlobRoot.fontFamily = fontFamily; 
            fontBlobRoot.fontSubFamily = fontSubFamily;
            fontBlobRoot.typographicFamily = typographicFamily;
            fontBlobRoot.typographicSubfamily = typographicSubfamily;            
            fontBlobRoot.fontAssetRef = new FontAssetRef(fontBlobRoot.fontFamily, fontBlobRoot.typographicFamily, weight, width, isItalic, slant);

            var result = builder.CreateBlobAssetReference<FontBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}