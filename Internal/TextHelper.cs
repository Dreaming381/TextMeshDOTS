using Unity.Collections;

namespace TextMeshDOTS
{
    public static class TextHelper
    {
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