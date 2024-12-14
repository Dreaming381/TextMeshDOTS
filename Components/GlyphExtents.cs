using System.Runtime.InteropServices;
using UnityEngine.TextCore;

namespace TextMeshDOTS
{
    /// <summary> Dimensions of glyph. As glyph is rendered to int x/y grid (texture), use of float in equivalent TextCore.GlyphMetrics makes no sense. </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphExtents
    {        
        public int x_bearing;
        public int y_bearing;
        public int width;
        public int height;
        public int size => height * width;
        public static explicit operator GlyphExtents(GlyphMetrics v) 
        { 
            return new GlyphExtents { 
                x_bearing = (int)v.horizontalBearingX, 
                y_bearing = (int)v.horizontalBearingY, 
                width = (int)v.width, 
                height = (int)v.height }; 
        }
        public override string ToString()
        {
            return $"x_bearing {x_bearing} y_bearing {y_bearing} width {width} height {height}";
        }
    }    
}

