using TextMeshDOTS.RichText;
using UnityEngine;

namespace TextMeshDOTS
{
    //use LayoutConfig2 to change case prior to hb-shape. Works only for latin text
    //Should this use cases really be in scope of TextMeshDOTS? 
    internal struct LayoutConfig2
    {
        public FontStyles m_fontStyles;

        public LayoutConfig2(in TextBaseConfiguration textBaseConfiguration)
        {
            m_fontStyles = textBaseConfiguration.fontStyles;
        }
        public void Reset(in TextBaseConfiguration textBaseConfiguration)
        { 
            m_fontStyles = textBaseConfiguration.fontStyles;
        }
        internal void Update(ref XMLTag tag)
        {
            switch (tag.tagType)
            {
                case TagType.AllCaps:
                case TagType.Uppercase:
                    {
                        if (tag.isClosing)
                            m_fontStyles &= ~FontStyles.UpperCase;
                        else
                            m_fontStyles |= FontStyles.UpperCase;
                    }
                    break;
                case TagType.Lowercase:
                    {
                        if (tag.isClosing)
                            m_fontStyles &= ~FontStyles.LowerCase;
                        else
                            m_fontStyles |= FontStyles.LowerCase;
                    }
                    break;
            }
        }
    }    
}