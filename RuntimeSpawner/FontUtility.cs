using UnityEngine;
using UnityEditor;
using System.IO;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using Font = TextMeshDOTS.HarfBuzz.Font;

namespace TextMeshDOTS.Authoring
{
    [CreateAssetMenu(fileName = "FontUtility", menuName = "TextMeshDOTS/FontUtility")]
    public class FontUtility : ScriptableObject
    {
        public Object font;
        public string fontAssetPath;
        public string fontFamily;
        public string fontSubFamily;
        public string typographicFamily;
        public string typographicSubfamily;
        public int weight;
        public int width;
        public string isItalic;
        public int slant;


#if UNITY_EDITOR
        //[MenuItem("TextMeshDOTS/Extract font data")]
        [MenuItem("CONTEXT/FontUtility/Extract font data")]
        static void ExtractFileNames()
        {
            var activeObject = Selection.activeObject;
            if (activeObject == null || !(activeObject.GetType() == typeof(FontUtility)))
            {
                Debug.LogError($"{activeObject.GetType()} is not FontUtility");
                return;
            }
            var fontUtility = activeObject as FontUtility;

            fontUtility.fontAssetPath = AssetDatabase.GetAssetPath(fontUtility.font);
            bool isTrueType = fontUtility.fontAssetPath.EndsWith("ttf", System.StringComparison.OrdinalIgnoreCase);
            bool isOpentype = fontUtility.fontAssetPath.EndsWith("otf", System.StringComparison.OrdinalIgnoreCase);
            if (isOpentype || isTrueType)
            {
                var fontBytes = File.ReadAllBytes(fontUtility.fontAssetPath);
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
                fontUtility.fontFamily = tmp.ToString();

                textSize = initialCapacity;
                face.GetFaceInfo(NameID.FONT_SUBFAMILY, language, ref textSize, ref tmp);
                tmp.Length = (int)textSize;
                fontUtility.fontSubFamily = tmp.ToString();

                textSize = initialCapacity;
                face.GetFaceInfo(NameID.TYPOGRAPHIC_FAMILY, language, ref textSize, ref tmp);
                tmp.Length = (int)textSize;
                fontUtility.typographicFamily = tmp.ToString();

                textSize = initialCapacity;
                face.GetFaceInfo(NameID.TYPOGRAPHIC_SUBFAMILY, language, ref textSize, ref tmp);
                tmp.Length = (int)textSize;
                fontUtility.typographicSubfamily = tmp.ToString();

                fontUtility.weight = (int)font.GetStyleTag(StyleTag.WEIGHT);
                fontUtility.width = (int)font.GetStyleTag(StyleTag.WIDTH);
                var italic = (byte)font.GetStyleTag(StyleTag.ITALIC);
                fontUtility.isItalic = italic == 1 ? "true" : "false";                
                fontUtility.slant = (int)font.GetStyleTag(StyleTag.SLANT_ANGLE);
                font.Dispose();
                face.Dispose();
                blob.Dispose();
            }
            else
            {
                Debug.LogWarning("Ensure you only have files ending with 'ttf' or 'otf' (case insensitiv) in font list");
                return;
            }            
        }
#endif
    }
}
