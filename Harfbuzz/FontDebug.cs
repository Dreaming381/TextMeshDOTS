using System;
using Unity.Collections;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz
{
    public class FontDebug
    {
        public static void GetNameTags(Face face)
        {
            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));
            var result = new FixedString128Bytes();
            var values = Enum.GetValues(typeof(NameID));
            foreach (NameID value in values)
            {
                var textSize = (uint)result.Capacity;
                face.GetFaceInfo(value, language, ref textSize, ref result);
                result.Length = (int)textSize;
                Debug.Log($"{value}: {result}");
                result.Clear();
            }
        }
    }
}
