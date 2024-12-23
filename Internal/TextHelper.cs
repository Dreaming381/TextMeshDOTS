using HarfBuzz;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS
{
    public static class TextHelper
    {
        //public static int GetFontIndex(DynamicBuffer<FontMaterial> fontMaterial, int familyNameHashCode, TextFontWeight textFontWeight, bool isItalic)
        //{
        //    var fontAssetRef = new FontAssetRef(familyNameHashCode, textFontWeight, isItalic);

        //    for (int i = 0, lenght = fontMaterial.Length; i < lenght; i++)
        //    {
        //        //Debug.Log($"Testing {fontMaterial[i].fontBlob.familyName} {fontMaterial[i].fontBlob.styleName}");
        //        if (fontMaterial[i].dynamicFontBlob.fontAssetRef == fontAssetRef)
        //        {
        //            //Debug.Log($"Match at index {i}");
        //            return i;
        //        }
        //    }
        //    return -1;
        //}
        public static int GetFontIndex(FontAssetArray fontAssetArray, FontAssetRef desiredFontAssetRef)
        {            
            var fontAssetRefs = fontAssetArray.fontAssetRefs;
            for (int i = 0, lenght = fontAssetRefs.Length; i < lenght; i++)
            {
                if (fontAssetArray.fontAssetRefs[i] == desiredFontAssetRef)
                    return i;
            }
            return -1;
        }
        public static int GetHashCodeCaseInSensitive(FixedString128Bytes text)
        {
            var s = text.GetEnumerator();
            int num = 0;
            while (s.MoveNext())
            {
                num = ((num << 5) + num) ^ s.Current.ToUpper().value;
                ;
            }
            return num;
        }
    }
}