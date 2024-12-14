using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS
{
    internal static class TextHelper
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
        public static int GetFontIndex(DynamicBuffer<FontEntity> fontEntities, ComponentLookup<HBFontPointer> hbFontPointerLookup, int familyNameHashCode, TextFontWeight textFontWeight, bool isItalic)
        {
            var fontAssetRef = new FontAssetRef(familyNameHashCode, textFontWeight, isItalic);

            for (int i = 0, lenght = fontEntities.Length; i < lenght; i++)
            {
                var fontEntity = fontEntities[i].value;
                //Debug.Log($"Testing {fontMaterial[i].fontBlob.familyName} {fontMaterial[i].fontBlob.styleName}");
                if (hbFontPointerLookup[fontEntity].fontAssetRef == fontAssetRef)
                {
                    //Debug.Log($"Match at index {i}");
                    return i;
                }
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

