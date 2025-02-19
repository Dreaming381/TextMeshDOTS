using System;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.RichText;
using Unity.Collections;

namespace TextMeshDOTS
{
    internal struct OpenTypeFeatureConfig : IDisposable
    {
        //https://learn.microsoft.com/en-us/typography/opentype/spec/featurelist

        public NativeList<Feature> values;
        public int smallCapsStartID;
        public int subscriptStartID;
        public int superscriptStartID;
        public int fractionStartID;
        internal OpenTypeFeatureConfig(int size, Allocator allocator)
        {
            values = new NativeList<Feature>(size, allocator);
            smallCapsStartID = -1;
            subscriptStartID = -1;
            superscriptStartID = -1;
            fractionStartID = -1;
        }
        internal void FinalizeOpenTypeFeatures(uint position)
        {
            if (smallCapsStartID != -1)
                values.Add(new Feature(HB.HB_TAG('s', 'm', 'c', 'p'), 1, (uint)smallCapsStartID, position));
            if (subscriptStartID != -1)
                values.Add(new Feature(HB.HB_TAG('s', 'u', 'b', 's'), 1, (uint)subscriptStartID, position));
            if (superscriptStartID != -1)
                values.Add(new Feature(HB.HB_TAG('s', 'u', 'p', 's'), 1, (uint)superscriptStartID, position));
            if (fractionStartID != -1)
                values.Add(new Feature(HB.HB_TAG('f', 'r', 'a', 'c'), 1, (uint)fractionStartID, position));
        }
        internal void Update(ref XMLTag tag)
        {
            switch (tag.tagType)
            {
                case TagType.SmallCaps:
                    if (!tag.isClosing)
                    {
                        if (smallCapsStartID == -1)
                            smallCapsStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('s', 'm', 'c', 'p'), 1, (uint)smallCapsStartID, (uint)tag.position));
                        smallCapsStartID = -1;
                    }
                    return;
                case TagType.Subscript:
                    if (!tag.isClosing)
                    {
                        if (subscriptStartID == -1)
                            subscriptStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('s', 'u', 'b', 's'), 1, (uint)subscriptStartID, (uint)tag.position));
                        subscriptStartID = -1;
                    }
                    return;
                case TagType.Superscript:
                    if (!tag.isClosing)
                    {
                        if (superscriptStartID == -1)
                            superscriptStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('s', 'u', 'p', 's'), 1, (uint)superscriptStartID, (uint)tag.position));
                        superscriptStartID = -1;
                    }
                    return;
                case TagType.Fraction:
                    if (!tag.isClosing)
                    {
                        if (fractionStartID == -1)
                            fractionStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('f', 'r', 'a', 'c'), 1, (uint)fractionStartID, (uint)tag.position));
                        fractionStartID = -1;
                    }
                    return;
            }

        }
        public void SetGlobalFeatures(in TextBaseConfiguration textBaseConfiguration, uint textLendth)
        {
            if ((textBaseConfiguration.fontStyles & FontStyles.SmallCaps) == FontStyles.SmallCaps)
                values.Add(new Feature(HB.HB_TAG('s', 'm', 'c', 'p'), 1, 0, textLendth));
            if ((textBaseConfiguration.fontStyles & FontStyles.Subscript) == FontStyles.Subscript)
                values.Add(new Feature(HB.HB_TAG('s', 'u', 'b', 's'), 1, 0, textLendth));
            if ((textBaseConfiguration.fontStyles & FontStyles.Superscript) == FontStyles.Superscript)
                values.Add(new Feature(HB.HB_TAG('s', 'u', 'p', 's'), 1, 0, textLendth));
            if ((textBaseConfiguration.fontStyles & FontStyles.Fraction) == FontStyles.Fraction)
                values.Add(new Feature(HB.HB_TAG('f', 'r', 'a', 'c'), 1, 0, textLendth));
        }
        public void Clear()
        {
            values.Clear();
            smallCapsStartID = -1;
            subscriptStartID = -1;
            superscriptStartID = -1;
            fractionStartID = -1;
        }

        public void Dispose()
        {
            if (values.IsCreated) values.Dispose();
        }
    }       
}