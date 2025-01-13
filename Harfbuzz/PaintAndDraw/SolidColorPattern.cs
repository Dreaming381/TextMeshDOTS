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
        /// <summary>
        /// For a given vertex (/object space pixel) of the rendered glyph, this method calculates the UV coordinates that 
        /// a texture of the color gradient would have. 'Solid fill' has same color for for every UV, so can do shortcut here.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColorARGB GetColor(float x, float y)
        {
            return m_colorARGB;
        }
    }
}
