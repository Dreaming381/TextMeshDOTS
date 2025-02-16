using TextMeshDOTS.RichText;
using Unity.Collections;

namespace TextMeshDOTS
{    
    internal struct FontConfig
    {
        public int currentFontMaterialIndex;

        public FontStyles fontStyles;

        public int fontFamilyHash;
        public FixedStack512Bytes<int> fontFamilyHashStack;

        public int m_fontWeight;
        public FixedStack512Bytes<int> fontWeightStack;
        public void Reset(in TextBaseConfiguration textBaseConfiguration, ref FontAssetArray fontAssetArray)
        {
            fontStyles = textBaseConfiguration.fontStyles;

            fontFamilyHash = fontAssetArray.fontAssetRefs[0].familyHash;
            fontFamilyHashStack.Clear();
            fontFamilyHashStack.Add(fontFamilyHash);

            //find font Entity requested by combination of font family and style
            var desiredFontAssetRef = new FontAssetRef(fontFamilyHash, textBaseConfiguration.fontStyles);
            var fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
            currentFontMaterialIndex = fontIndex == -1 ? 0 : fontIndex;
            //Debug.Log($"Initialize to fontID {currentFontMaterialIndex}");

            m_fontWeight = 0;// to-do: initialize based on textBaseConfiguration
            fontWeightStack.Clear();
            fontWeightStack.Add(m_fontWeight);
        }
        internal void GetCurrentFontIndex(ref XMLTag tag, ref FontAssetArray fontAssetArray, ref CalliString calliStringRaw)
        {
            FontAssetRef desiredFontAssetRef;
            int fontIndex;
            switch (tag.tagType)
            {
                case TagType.Bold:
                    if (!tag.isClosing)
                        fontStyles |= FontStyles.Bold;
                    else
                        fontStyles &= ~FontStyles.Bold;
                    desiredFontAssetRef = new FontAssetRef(fontFamilyHash, fontStyles);
                    fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
                    if (fontIndex != -1)
                        currentFontMaterialIndex = fontIndex;
                    return;
                case TagType.Italic:
                    if (!tag.isClosing)
                        fontStyles |= FontStyles.Italic;
                    else
                        fontStyles &= ~FontStyles.Italic;
                    desiredFontAssetRef = new FontAssetRef(fontFamilyHash, fontStyles);
                    fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
                    if (fontIndex != -1)
                        currentFontMaterialIndex = fontIndex;
                    return;
                case TagType.FontWeight:
                    desiredFontAssetRef = new FontAssetRef(fontFamilyHash, fontStyles);
                    if (!tag.isClosing)
                    {
                        desiredFontAssetRef.weight = (int)tag.value.NumericalValue;
                        fontWeightStack.Add(desiredFontAssetRef.weight);
                    }
                    else
                        desiredFontAssetRef.weight = fontWeightStack.RemoveExceptRoot();
                    fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
                    if (fontIndex != -1)
                        currentFontMaterialIndex = fontIndex;
                    return;
                case TagType.Font:
                    if (!tag.isClosing)
                    {
                        if (tag.value.stringValue == StringValue.Default)
                        {
                            fontFamilyHash = fontFamilyHashStack[0];
                            desiredFontAssetRef = new FontAssetRef(fontFamilyHash, fontStyles);
                            fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
                            if (fontIndex != -1)
                                currentFontMaterialIndex = fontIndex;
                            return;
                        }
                        //fetch name of font from calliStringRaw Buffer
                        FixedString128Bytes stringValue = default; //should not happen too often, so should be OK to allocate here
                        calliStringRaw.GetSubString(ref stringValue, tag.value.valueStart, tag.value.valueLength);
                        var desiredFontFamilyHash = TextHelper.GetHashCodeCaseInSensitive(stringValue);
                        desiredFontAssetRef = new FontAssetRef(desiredFontFamilyHash, fontStyles);
                        fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
                        if (fontIndex != -1)
                        {
                            fontFamilyHash = desiredFontFamilyHash;
                            fontFamilyHashStack.Add(desiredFontFamilyHash);
                            currentFontMaterialIndex = fontIndex;
                            return;
                        }
                    }
                    else
                    {
                        fontFamilyHash = fontFamilyHashStack.RemoveExceptRoot();
                        desiredFontAssetRef = new FontAssetRef(fontFamilyHash, fontStyles);
                        fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
                        if (fontIndex != -1)
                            currentFontMaterialIndex = fontIndex;
                    }
                    return;
            }
        }
    }    
}