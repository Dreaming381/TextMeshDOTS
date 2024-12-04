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


        public static int GetTextFontWeightIndex(TextFontWeight textFontWeight)
        {
            return TextUtilities.GetTextFontWeightIndex(textFontWeight);
        }
        
        //public static void ListSomeInfo(this FontAsset font)
        //{
        //    Debug.Log($"font.m_SourceFontFilePath {font.m_SourceFontFilePath}");
        //    Debug.Log($"font.sourceFontFile.name {font.sourceFontFile.name}");
        //    Debug.Log($"font.m_SourceFontFilePath {font.name}");
        //    Debug.Log($"font.InternalDynamicOS {font.InternalDynamicOS}");

        //    //FontEngine.

        //    Debug.Log($"nativeFontAsset {font.nativeFontAsset != IntPtr.Zero}");
        //    Debug.Log($"font.familyNameHashCode {font.familyNameHashCode}");
        //    Debug.Log($"font.styleNameHashCode {font.styleNameHashCode}");
        //}
        //public static int GetFaceIndex(this FaceInfo faceInfo)
        //{
        //    return faceInfo.faceIndex;
        //}

        //public static FontEngineError LoadFontFace(this FontAsset font)
        //{

        //    if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
        //    {
        //        //// Font Asset should have a valid reference to a font in the Editor.
        //        //if (font.sourceFontFile == null)
        //        //    font.sourceFontFile = font.SourceFont_EditorRef;

        //        // Try loading the font face from source font object
        //        if (FontEngine.LoadFontFace(font.sourceFontFile, m_FaceInfo.pointSize, m_FaceInfo.faceIndex) == FontEngineError.Success)
        //            return FontEngineError.Success;

        //        // Try loading the font face from file path
        //        if (string.IsNullOrEmpty(font.m_SourceFontFilePath) == false)
        //            return FontEngine.LoadFontFace(font.m_SourceFontFilePath, font.m_FaceInfo.pointSize, font.m_FaceInfo.faceIndex);

        //        return FontEngineError.Invalid_Face;
        //    }
        //    return FontEngineError.Atlas_Generation_Cancelled;
        //}

        public static bool s_initialized = false;

        public static float GetPaddingForText(this Material material, bool enableExtraPadding, bool isBold)
        {
            if (!s_initialized)
                TextShaderUtilities.GetShaderPropertyIDs();

            return TextShaderUtilities.GetPadding(material, enableExtraPadding, isBold);
        }
    }
}

