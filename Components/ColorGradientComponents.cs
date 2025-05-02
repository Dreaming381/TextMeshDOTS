using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS
{
    /// <summary>
    /// Definition of color gradients
    /// </summary>
    [Serializable]
    public struct TextMeshDOTSColorGradient
    {
        [SerializeField]
        public string name;
        [SerializeField]
        public ColorGradientMode colorMode;
        [SerializeField]
        public Color topLeft;
        [SerializeField]
        public Color topRight;
        [SerializeField]
        public Color bottomLeft;
        [SerializeField]
        public Color bottomRight;
    }
    /// <summary>
    /// Definition of color gradients
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TextColorGradient : IBufferElementData
    {
        public int nameHash;
        public Color32 topLeft;
        public Color32 topRight;
        public Color32 bottomLeft;
        public Color32 bottomRight;
    }
}
