using System;
using System.Collections.Generic;
using System.IO;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;
using Object = UnityEngine.Object;

namespace TextMeshDOTS.Authoring
{
    [CreateAssetMenu(fileName = "FontCollectionAsset", menuName = "TextMeshDOTS/Font Collection Asset")]
    public class FontCollectionAsset : ScriptableObject
    {
        [Tooltip("Supported types: .otf .ttf .ttc")]
        public List<Object> systemFonts;
        [Tooltip("Supported types: .otf .ttf .ttc")]
        public List<Object> streamingAssetFonts;
        public List<FontInfo> fontInfos;
        public List<string> fontFamilies;
        public void ProcessFonts()
        {
            Debug.Log("Process Fonts");
            if (fontInfos == null)
                fontInfos = new List<FontInfo>(streamingAssetFonts.Count);
            else
                fontInfos.Clear();

            for (int i = 0, ii = systemFonts.Count; i < ii; i++)
            {
                if (GetFontInfo(systemFonts[i], true, out FontInfo fontInfo))
                    fontInfos.Add(fontInfo);
                else
                {
                    fontInfos.Clear();
                    return;
                }
            }

            for (int i = 0, ii = streamingAssetFonts.Count; i < ii; i++)
            {
                if (GetFontInfo(streamingAssetFonts[i], false, out FontInfo fontInfo))
                    fontInfos.Add(fontInfo);
                else
                {
                    fontInfos.Clear();
                    return;
                }
            }

            if (fontFamilies == null)
                fontFamilies = new List<string>(fontInfos.Count);
            else
                fontFamilies.Clear();
            for (int i = 0, ii = fontInfos.Count; i < ii; i++)
            {
                var fontInfo = fontInfos[i];
                var fontFamily = fontInfo.typographicFamily == String.Empty ? fontInfo.fontFamily : fontInfo.typographicFamily;
                if (!fontFamilies.Contains(fontFamily))
                    fontFamilies.Add(fontFamily);
            }
            //ensure values are serialized
            EditorUtility.SetDirty(this);
        }
        bool GetFontInfo(Object fontItem, bool systemFont, out FontInfo fontInfo)
        {
            var fontAssetPath = AssetDatabase.GetAssetPath(fontItem);
            fontInfo = new FontInfo();
            bool isTrueType = fontAssetPath.EndsWith("ttf", System.StringComparison.OrdinalIgnoreCase);
            bool isOpentype = fontAssetPath.EndsWith("otf", System.StringComparison.OrdinalIgnoreCase);
            fontInfo.fontAssetPath = systemFont ? "" : fontAssetPath;
            if (isOpentype || isTrueType)
            {
                var fontBytes = File.ReadAllBytes(fontAssetPath);
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
                var tmp = new FixedString128Bytes();
                uint textSize = initialCapacity;
                face.GetFaceInfo(NameID.FONT_FAMILY, language, ref textSize, ref tmp);
                tmp.Length = (int)textSize;
                fontInfo.fontFamily = tmp.ToString();

                textSize = initialCapacity;
                face.GetFaceInfo(NameID.FONT_SUBFAMILY, language, ref textSize, ref tmp);
                tmp.Length = (int)textSize;
                fontInfo.fontSubFamily = tmp.ToString();

                textSize = initialCapacity;
                face.GetFaceInfo(NameID.TYPOGRAPHIC_FAMILY, language, ref textSize, ref tmp);
                tmp.Length = (int)textSize;
                fontInfo.typographicFamily = tmp.ToString();

                textSize = initialCapacity;
                face.GetFaceInfo(NameID.TYPOGRAPHIC_SUBFAMILY, language, ref textSize, ref tmp);
                tmp.Length = (int)textSize;
                fontInfo.typographicSubfamily = tmp.ToString();

                fontInfo.weight = (int)font.GetStyleTag(StyleTag.WEIGHT);
                fontInfo.width = (int)font.GetStyleTag(StyleTag.WIDTH);
                var italic = (byte)font.GetStyleTag(StyleTag.ITALIC);
                fontInfo.isItalic = italic == 1 ? true : false;
                fontInfo.slant = (int)font.GetStyleTag(StyleTag.SLANT_ANGLE);
                font.Dispose();
                face.Dispose();
                blob.Dispose();
                return true;
            }
            else
            {
                Debug.LogWarning("Ensure you only have files ending with 'ttf' or 'otf' (case insensitiv) in font list");
                return false;
            }
        }
    }
    [Serializable]
    public struct FontInfo
    {
        [SerializeField]
        public string fontAssetPath;
        [SerializeField]
        public string fontFamily;
        [SerializeField]
        public string fontSubFamily;
        [SerializeField]
        public string typographicFamily;
        [SerializeField]
        public string typographicSubfamily;
        [SerializeField]
        public int weight;
        [SerializeField]
        public int width;
        [SerializeField]
        public bool isItalic;
        [SerializeField]
        public int slant;
    }
}
