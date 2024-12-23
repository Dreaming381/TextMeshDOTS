using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS
{    
    public static class TextCoreExtensions
    {
        //public static int GetHashCodeCaseInSensitive(string text)
        //{
        //    return TextUtilities.GetHashCodeCaseInSensitive(text);
        //}
        public static bool TryAddGlyphInternal(this FontAsset font, uint glyphIndex, out Glyph glyph)
        {
            return font.TryAddGlyphInternal(glyphIndex, out glyph);
        }
        public static bool TryGetGlyph(this FontAsset font, uint glyphIndex, out Glyph glyph)
        {
            return font.glyphLookupTable.TryGetValue(glyphIndex, out glyph);
        }
        public static List<UnityFontReference> GetSystemFontRef()
        {
            var fontReferences = FontEngine.GetSystemFontReferences();
            var unityFontReferences = new List<UnityFontReference>(fontReferences.Length);
            for (int i = 0; i < fontReferences.Length; i++)
            {
                var m_fontRef = fontReferences[i];
                unityFontReferences.Add(new UnityFontReference {typographicFamily= m_fontRef.familyName, typographicSubfamily= m_fontRef.styleName, faceIndex= m_fontRef.faceIndex, filePath= m_fontRef.filePath } );
            }
            unityFontReferences.Sort(default(UnityFontReferenceComparer));
            return unityFontReferences;
        }

        public static bool TryGetSystemFontReference(string familyName, string styleName, out UnityFontReference unityFontReference)
        {
            var success = FontEngine.TryGetSystemFontReference(familyName, styleName, out FontReference m_fontRef);
            unityFontReference = success ? new UnityFontReference { typographicFamily = m_fontRef.familyName, typographicSubfamily = m_fontRef.styleName, faceIndex = m_fontRef.faceIndex, filePath = m_fontRef.filePath } : default;
            return success;
        }

        public static int GetTextFontWeightIndex(TextFontWeight textFontWeight)
        {
            return TextUtilities.GetTextFontWeightIndex(textFontWeight);
        }  

        public static bool s_initialized = false;

        public static float GetPaddingForText(this Material material, bool enableExtraPadding, bool isBold)
        {
            if (!s_initialized)
                TextShaderUtilities.GetShaderPropertyIDs();

            return TextShaderUtilities.GetPadding(material, enableExtraPadding, isBold);
        }
        public struct UnityFontReference
        {
            public string typographicFamily;

            public string typographicSubfamily;

            public int faceIndex;

            public string filePath;
        }
        public struct UnityFontReferenceComparer : IComparer<UnityFontReference>
        {
            public int Compare(UnityFontReference a, UnityFontReference b)
            {
                return a.typographicFamily.CompareTo(b.typographicFamily);                
            }
        }
    }    
}

