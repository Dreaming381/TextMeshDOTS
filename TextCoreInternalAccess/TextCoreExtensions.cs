using System.Collections.Generic;
using UnityEngine.TextCore.LowLevel;

namespace TextMeshDOTS
{    
    public static class TextCoreExtensions
    {
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
            //var test = UnityEngine.Font.GetPathsToOSFonts();
            //var test2 = UnityEngine.Font.GetOSInstalledFontNames();
            var success = FontEngine.TryGetSystemFontReference(familyName, styleName, out FontReference m_fontRef);
            unityFontReference = success ? new UnityFontReference { typographicFamily = m_fontRef.familyName, typographicSubfamily = m_fontRef.styleName, faceIndex = m_fontRef.faceIndex, filePath = m_fontRef.filePath } : default;
            return success;
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

