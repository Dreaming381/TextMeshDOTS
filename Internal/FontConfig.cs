using TextMeshDOTS.RichText;
using Unity.Collections;
using UnityEngine;

namespace TextMeshDOTS
{    
    internal struct FontConfig
    {
        public int m_fontMaterialIndex;        

        public int m_fontFamilyHash;
        public FixedStack512Bytes<int> m_fontFamilyHashStack;

        public FontWeight m_fontWeight;
        public FixedStack512Bytes<FontWeight> m_fontWeightStack;

        public float m_fontWidth;
        public FixedStack512Bytes<float> m_fontWidthStack;

        public FontStyles m_fontStyles; //only used for italic state
        public void Reset(in TextBaseConfiguration textBaseConfiguration, ref FontAssetArray fontAssetArray)
        {
            m_fontStyles = textBaseConfiguration.fontStyles;

            m_fontFamilyHash = fontAssetArray.fontAssetRefs[0].familyHash;
            m_fontFamilyHashStack.Clear();
            m_fontFamilyHashStack.Add(m_fontFamilyHash);

            m_fontWeight = textBaseConfiguration.fontWeight;
            m_fontWeightStack.Clear();
            m_fontWeightStack.Add(m_fontWeight);

            m_fontWidth = textBaseConfiguration.fontWidth;
            m_fontWidthStack.Clear();
            m_fontWidthStack.Add(m_fontWidth);

            m_fontMaterialIndex = 0;

            var desiredFontAssetRef = new FontAssetRef(m_fontFamilyHash, m_fontWeight, m_fontWidth, m_fontStyles);
            var fontIndex = fontAssetArray.GetFontIndex(desiredFontAssetRef);
            m_fontMaterialIndex = fontIndex == -1 ? 0 : fontIndex;
        }        
        public int GetFontIndex(ref FontAssetArray fontAssetArray)
        {
            var desiredFontAssetRef = new FontAssetRef
            {
                familyHash = m_fontFamilyHash,
                weight = m_fontWeight,
                width = m_fontWidth,
                isItalic = (m_fontStyles & FontStyles.Italic) == FontStyles.Italic,
                slant = 0,
            };
            return fontAssetArray.GetFontIndex(desiredFontAssetRef);
        }
        
        /// <summary>
        /// In case the XMLTag  causes a change of the required font by changing any of the parameters in FontAssetRef, this method 
        /// searches the index of that font (0=main entity, >0 AdditionalFontEntity) in the provided FontAssetArray 
        /// </summary>
        internal void GetCurrentFontIndex(ref XMLTag tag, ref FontAssetArray fontAssetArray, ref CalliString calliStringRaw)
        {
            int fontIndex;
            switch (tag.tagType)
            {
                case TagType.Italic:
                    if (!tag.isClosing)
                        m_fontStyles |= FontStyles.Italic;
                    else
                        m_fontStyles &= ~FontStyles.Italic;
                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        m_fontMaterialIndex = fontIndex;
                    return;
                case TagType.Bold:
                    if (!tag.isClosing)
                    {
                        m_fontWeight = FontWeight.Bold;
                        m_fontWeightStack.Add(m_fontWeight);
                    }
                    else
                        m_fontWeight = m_fontWeightStack.RemoveExceptRoot();
                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        m_fontMaterialIndex = fontIndex;
                    return;                
                case TagType.FontWeight:                   
                    if (!tag.isClosing)
                    {
                        m_fontWeight = (FontWeight)tag.value.NumericalValue;
                        m_fontWeightStack.Add(m_fontWeight);
                    }
                    else
                        m_fontWeight = m_fontWeightStack.RemoveExceptRoot();

                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        m_fontMaterialIndex = fontIndex;
                    return;
                case TagType.FontWidth:
                    if (!tag.isClosing)
                    {
                        m_fontWidth = tag.value.NumericalValue;
                        m_fontWidthStack.Add(m_fontWidth);
                    }
                    else
                        m_fontWidth = m_fontWidthStack.RemoveExceptRoot();

                    fontIndex = GetFontIndex(ref fontAssetArray);
                    if (fontIndex != -1)
                        m_fontMaterialIndex = fontIndex;
                    return;
                case TagType.Font:
                    if (!tag.isClosing)
                    {
                        if (tag.value.stringValue == StringValue.Default)
                            m_fontFamilyHash = m_fontFamilyHashStack[0];                        
                        else 
                        {
                            //fetch name of font from calliStringRaw Buffer
                            //To-Do: better to store valueHash in tag struct
                            FixedString128Bytes stringValue = default; //should not happen too often, so should be OK to allocate here
                            calliStringRaw.GetSubString(ref stringValue, tag.value.valueStart, tag.value.valueLength);
                            m_fontFamilyHash = TextHelper.GetHashCodeCaseInSensitive(stringValue); 
                            m_fontFamilyHashStack.Add(m_fontFamilyHash);
                        }
                        fontIndex = GetFontIndex(ref fontAssetArray);
                        if (fontIndex != -1)
                            m_fontMaterialIndex = fontIndex;
                        return;                        
                    }
                    else
                    {
                        m_fontFamilyHash = m_fontFamilyHashStack.RemoveExceptRoot();
                        fontIndex = GetFontIndex(ref fontAssetArray);
                        if (fontIndex != -1)
                            m_fontMaterialIndex = fontIndex;
                    }
                    return;
            }
        }
    }    
}