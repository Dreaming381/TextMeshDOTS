using Unity.Collections;
using UnityEngine;

namespace TextMeshDOTS
{
    public static class TextHelper
    {
        /// <summary> Find font entity requested by combination of font family and style </summary>
        public static int GetFontIndex(FontAssetArray fontAssetArray, FontAssetRef desiredFontAssetRef)
        {            
            var fontAssetRefs = fontAssetArray.fontAssetRefs;
            for (int i = 0, lenght = fontAssetRefs.Length; i < lenght; i++)
            {
                //Debug.Log($"current: {fontAssetArray.fontAssetRefs[i].ToString()}");
                if (fontAssetArray.fontAssetRefs[i] == desiredFontAssetRef)
                    return i;
            }

            //fall back to family in case we end up here
            for (int i = 0, lenght = fontAssetRefs.Length; i < lenght; i++)
            {
                //Debug.Log($"current: {fontAssetArray.fontAssetRefs[i].ToString()}");
                if (fontAssetArray.fontAssetRefs[i].familyHash == desiredFontAssetRef.familyHash)
                    return i;
            }
            //Debug.Log($"Requested font not found");
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