using Unity.Collections;
using UnityEngine;

namespace TextMeshDOTS.RichText
{
    internal struct TagTypeInfo
    {
        internal TagTypeInfo(TagType tagType, string name, TagValueType valueType = TagValueType.None, TagUnitType unitType = TagUnitType.Pixels)
        {
            TagType = tagType;
            this.name = name;
            this.valueType = valueType;
            this.unitType = unitType;
        }

        public TagType TagType;
        public FixedString32Bytes name;
        public TagValueType valueType;
        public TagUnitType unitType;
    }
}
