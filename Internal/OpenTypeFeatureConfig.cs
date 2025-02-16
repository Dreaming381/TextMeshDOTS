using System;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.RichText;
using Unity.Collections;
using UnityEngine;

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
            smallCapsStartID = 0;
            subscriptStartID = 0;
            superscriptStartID = 0;
            fractionStartID = 0;
        }
        internal void FinalizeOpenTypeFeatures(uint position)
        {
            if (smallCapsStartID != 0)
                values.Add(new Feature(HB.HB_TAG('s', 'm', 'c', 'p'), 1, (uint)smallCapsStartID, position));
            if (subscriptStartID != 0)
                values.Add(new Feature(HB.HB_TAG('s', 'u', 'b', 's'), 1, (uint)subscriptStartID, position));
            if (superscriptStartID != 0)
                values.Add(new Feature(HB.HB_TAG('s', 'u', 'p', 's'), 1, (uint)superscriptStartID, position));
            if (fractionStartID != 0)
                values.Add(new Feature(HB.HB_TAG('f', 'r', 'a', 'c'), 1, (uint)fractionStartID, position));
        }
        internal void UpdateOpenTypeFeatures(ref XMLTag tag)
        {
            switch (tag.tagType)
            {
                case TagType.SmallCaps:
                    if (!tag.isClosing)
                    {
                        if (smallCapsStartID == 0)
                            smallCapsStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('s', 'm', 'c', 'p'), 1, (uint)smallCapsStartID, (uint)tag.position));
                        smallCapsStartID = 0;
                    }
                    return;
                case TagType.Subscript:
                    if (!tag.isClosing)
                    {
                        if (subscriptStartID == 0)
                            subscriptStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('s', 'u', 'b', 's'), 1, (uint)subscriptStartID, (uint)tag.position));
                        subscriptStartID = 0;
                    }
                    return;
                case TagType.Superscript:
                    if (!tag.isClosing)
                    {
                        if (superscriptStartID == 0)
                            superscriptStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('s', 'u', 'p', 's'), 1, (uint)superscriptStartID, (uint)tag.position));
                        superscriptStartID = 0;
                    }
                    return;
                case TagType.Fraction:
                    if (!tag.isClosing)
                    {
                        if (fractionStartID == 0)
                            fractionStartID = tag.position;
                    }
                    else
                    {
                        values.Add(new Feature(HB.HB_TAG('f', 'r', 'a', 'c'), 1, (uint)fractionStartID, (uint)tag.position));
                        fractionStartID = 0;
                    }
                    return;
            }

        }
        public void Clear()
        {
            values.Clear();
            smallCapsStartID = 0;
            subscriptStartID = 0;
            superscriptStartID = 0;
            fractionStartID = 0;
        }

        public void Dispose()
        {
            if (values.IsCreated) values.Dispose();
        }
    }       
}