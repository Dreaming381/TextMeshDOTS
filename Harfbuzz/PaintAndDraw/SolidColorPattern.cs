using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz
{
    public struct SolidColor : IPattern
    {
        ColorARGB m_colorARGB;
        public SolidColor(ColorARGB colorARGB)
        {
            m_colorARGB = colorARGB;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColorARGB GetColor(float x, float y)
        {
            return m_colorARGB;
        }
    }
}
