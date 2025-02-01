using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine.TextCore;

namespace TextMeshDOTS
{
    /// <summary> Dimensions of glyph according to harfbuzz definition (y is top to bottom ). Invert height for use in a coordinate systems that grows up.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphExtents
    {        
        public int x_bearing;   //Distance from the x-origin to the left extremum of the glyph.
        public int y_bearing;   //Distance from the top extremum of the glyph to the y-origin.
        public int width;       //Distance from the left extremum of the glyph to the right extremum.
        public int height;      //Distance from the top extremum of the glyph to the bottom extremum.           
        public void InvertY()
        {
            height = -height; //Invert height for use in a coordinate systems that grows up.</ summary >
        }
        public GlyphRect GetPaddedAtlasRect(int x, int y, int padding)
        {
            return new GlyphRect(x, y, width + 2*padding, height +2 * padding);
        }
        public float2 GetOffsetToZero()
        {
            return new float2(x_bearing, height-y_bearing);
        }
        public int size => height * width;
        public override string ToString()
        {
            return $"x_bearing {x_bearing} y_bearing {y_bearing} width {width} height {height}";
        }
    }    
}

