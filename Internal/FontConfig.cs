using TextMeshDOTS.RichText;
using Unity.Collections;
using UnityEngine;

namespace TextMeshDOTS
{    
    internal struct FontConfig
    {
        public int fontMaterialIndex;        

        public int fontFamilyHash;
        public FixedStack512Bytes<int> fontFamilyHashStack;

        public FontWeight fontWeight;
        public FixedStack512Bytes<FontWeight> fontWeightStack;

        public float fontWidth;
        public FixedStack512Bytes<float> fontWidthStack;

        public FontStyles fontStyles; //only used for italic state
        public void Reset(in TextBaseConfiguration textBaseConfiguration, ref FontAssetArray fontAssetArray)
        {
            fontStyles = textBaseConfiguration.fontStyles;

            fontFamilyHash = fontAssetArray.fontAssetRefs[0].familyHash;
            fontFamilyHashStack.Clear();
            fontFamilyHashStack.Add(fontFamilyHash);

            fontWeight = textBaseConfiguration.fontWeight;
            fontWeightStack.Clear();
            fontWeightStack.Add(fontWeight);

            fontWidth = textBaseConfiguration.fontWidth;
            fontWidthStack.Clear();
            fontWidthStack.Add(fontWidth);

            fontMaterialIndex = 0;

            var desiredFontAssetRef = new FontAssetRef(fontFamilyHash, fontWeight, fontWidth, fontStyles);
            var fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
            fontMaterialIndex = fontIndex == -1 ? 0 : fontIndex;
        }
        public FontAssetRef GetFontAssetRef()
        {
            return new FontAssetRef
            {
                familyHash = fontFamilyHash,
                weight = (int)fontWeight,
                width = fontWidth,
                isItalic = (fontStyles & FontStyles.Italic) == FontStyles.Italic,
                slant = 0,
            };       
        }
        public int GetFontIndex(ref FontAssetArray fontAssetArray)
        {
            var desiredFontAssetRef = new FontAssetRef
            {
                familyHash = fontFamilyHash,
                weight = (int)fontWeight,
                width = fontWidth,
                isItalic = (fontStyles & FontStyles.Italic) == FontStyles.Italic,
                slant = 0,
            };
            return fontAssetArray.GetFontIndex(desiredFontAssetRef);
        }
        internal void GetCurrentFontIndex(ref XMLTag tag, ref FontAssetArray fontAssetArray, ref CalliString calliStringRaw)
        {
            int fontIndex;
            switch (tag.tagType)
            {
                case TagType.Bold:
                    if (!tag.isClosing)
                    {
                        fontWeight = FontWeight.Bold;
                        fontWeightStack.Add(fontWeight);
                    }
                    else
                        fontWeight = fontWeightStack.RemoveExceptRoot();
                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        fontMaterialIndex = fontIndex;
                    return;
                case TagType.Italic:
                    if (!tag.isClosing)
                        fontStyles |= FontStyles.Italic;
                    else
                        fontStyles &= ~FontStyles.Italic;
                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        fontMaterialIndex = fontIndex;
                    return;
                case TagType.FontWeight:                   
                    if (!tag.isClosing)
                    {
                        fontWeight = (FontWeight)tag.value.NumericalValue;
                        fontWeightStack.Add(fontWeight);
                    }
                    else
                        fontWeight = fontWeightStack.RemoveExceptRoot();

                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        fontMaterialIndex = fontIndex;
                    return;
                case TagType.FontWidth:
                    if (!tag.isClosing)
                    {
                        fontWidth = tag.value.NumericalValue;
                        fontWidthStack.Add(fontWidth);
                    }
                    else
                        fontWidth = fontWidthStack.RemoveExceptRoot();

                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        fontMaterialIndex = fontIndex;
                    return;
                case TagType.Font:
                    if (!tag.isClosing)
                    {
                        if (tag.value.stringValue == StringValue.Default)
                            fontFamilyHash = fontFamilyHashStack[0];                        
                        else 
                        {
                            //fetch name of font from calliStringRaw Buffer
                            FixedString128Bytes stringValue = default; //should not happen too often, so should be OK to allocate here
                            calliStringRaw.GetSubString(ref stringValue, tag.value.valueStart, tag.value.valueLength);
                            fontFamilyHash = TextHelper.GetHashCodeCaseInSensitive(stringValue); 
                            fontFamilyHashStack.Add(fontFamilyHash);
                        }
                        fontIndex = GetFontIndex(ref fontAssetArray);
                        if (fontIndex != -1)
                            fontMaterialIndex = fontIndex;
                        return;                        
                    }
                    else
                    {
                        fontFamilyHash = fontFamilyHashStack.RemoveExceptRoot();
                        fontIndex = GetFontIndex(ref fontAssetArray);
                        if (fontIndex != -1)
                            fontMaterialIndex = fontIndex;
                    }
                    return;
            }
        }
    }    
}