using Unity.Mathematics;
using UnityEngine.UIElements;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    public static class Blending
    {
        public static ColorARGB Normal(ColorARGB source, ColorARGB destination)
        {
            int4 tmp = (int4)source * source.a / 255;
            tmp[0] = source.a;

            var result = tmp + (int4)destination * (255 - source.a) / 255;

            // r = s * sa + d*(1-sa)
            //var result = (ColorARGB)((int4)source * source.a / 255 + (int4)destination * (255 - source.a) / 255);
            return (ColorARGB)result;
        }
        public static ColorARGB SrcOver(ColorARGB source, ColorARGB destination)
        {
            // r = s + (1-sa)*d
            var result = (int4)source + (255 - source.a) * (int4)destination / 255;

            var alpha = source.a + destination.a * (255 - source.a) / 255;
            result[0] = alpha;
            return (ColorARGB)result;
        }       


        public static ColorARGB DstOver(ColorARGB source, ColorARGB destination)
        {
            // r = d + (1-da)*s
            var result = (int4)destination + (255 - destination.a) * (int4)source / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB SrcIn(ColorARGB source, ColorARGB destination)
        {
            // r = s * da
            var result = (int4)source * destination.a / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB DstIn(ColorARGB source, ColorARGB destination)
        {
            // r = d * sa
            var result = (int4)destination * source.a / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB SrcOut(ColorARGB source, ColorARGB destination)
        {
            // r = s * (1 - da)
            var result = (int4)source *  (255 - destination.a) / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB DstOut(ColorARGB source, ColorARGB destination)
        {
            // r = d * (1 - sa)
            var result = (int4)destination * (255 - source.a) / 255;
            return (ColorARGB)result;
        }
        
        public static ColorARGB SrcAtop(ColorARGB source, ColorARGB destination)
        {
            // r = s*da + d*(1-sa)
            var result = (int4)source * destination.a / 255 + (int4)destination* (255-source.a) / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB DstAtop(ColorARGB source, ColorARGB destination)
        {
            // r = d*sa + s*(1-da)
            var result = (int4)destination * source.a / 255 + (int4)source * (255 - destination.a) / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB Xor(ColorARGB source, ColorARGB destination)
        {
            // r = s*(1-da) + d*(1-sa)
            var result = (int4)source * (255- destination.a)/255 + (int4)destination * (255 - source.a) / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB Plus(ColorARGB source, ColorARGB destination)
        {
            // r = min(s + d, 1)
            var result = math.min((int4)source + (int4)destination, 255);
            return (ColorARGB)result;
        }
        public static ColorARGB Screen(ColorARGB source, ColorARGB destination)
        {
            // r = s + d - s*d
            var result = (int4)source + (int4)destination - (int4)source * (int4)destination / 255;
            return (ColorARGB)result;
        }
        public static ColorARGB Multiply(ColorARGB source, ColorARGB destination)
        {
            // r = s*(1-da) + d*(1-sa) + s*d
            var result = (ColorARGB)((int4)source * (255 - destination.a) / 255 + (int4)destination * (255 - source.a) / 255 + ((int4)source * (int4)destination / 255));
            return result;
        }
        public static ColorARGB ColorDodge(ColorARGB source, ColorARGB destination)
        {
            // a / (1 - d)
            var result = (int4)source / (255 - (int4)destination) ;
            return (ColorARGB)result;
        }
        public static ColorARGB ColorBurn(ColorARGB source, ColorARGB destination)
        {
            // 1 - (1 - s) / d
            var result = 255 - (255 - (int4)source) / (int4)destination;
            return (ColorARGB)result;
        }

        public static ColorARGB Overlay(ColorARGB source, ColorARGB destination)
        {
            // multiply or screen, depending on destination
            //multiply: // r = s*(1-da) + d*(1-sa) + s*d
            //screen: // r = s + d - s*d
            var result = math.select(255 - 2 * (255 - (int4)source) * (255 - (int4)destination), 
                                    2 * (int4)source * (int4)destination/255, 
                                    (int4)source < 127);
            return (ColorARGB)result;
        }

    }
    public enum HB_PAINT_COMPOSITE_MODE
    {
        //(d * (1 - s.a) + s * s.a );
        CLEAR,      // r = 0
        SRC,        // r = s
        DEST,       // r = d
        SRC_OVER,   // r = s + (1-sa)*d
        DEST_OVER,  // r = d + (1-da)*s
        SRC_IN,     // r = s * da
        DEST_IN,    // r = d * sa
        SRC_OUT,    // r = s * (1-da)
        DEST_OUT,   // r = d * (1-sa)
        SRC_ATOP,   // r = s*da + d*(1-sa)
        DEST_ATOP,  // r = d*sa + s*(1-da)
        XOR,        // r = s*(1-da) + d*(1-sa)
        PLUS,       // r = min(s + d, 1)
        //MODULATE,      // r = s*d
        SCREEN,     // r = s + d - s*d
        OVERLAY,    // multiply or screen, depending on destination
        DARKEN,     // rc = s + d - max(s*da, d*sa), ra = kSrcOver
        LIGHTEN,    // rc = s + d - min(s*da, d*sa), ra = kSrcOver
        COLOR_DODGE,// a / (1 - b) brighten destination to reflect source
        COLOR_BURN, // 1 - (1 - a) / b darken destination to reflect source
        HARD_LIGHT, // multiply or screen, depending on source
        SOFT_LIGHT, // lighten or darken, depending on source
        DIFFERENCE, // rc = s + d - 2*(min(s*da, d*sa)), ra = kSrcOver
        EXCLUSION,  // rc = s + d - 2*(min(s*da, d*sa)), ra = kSrcOver
        MULTIPLY,   // r = s*(1-da) + d*(1-sa) + s*d
        HSL_HUE,        // hue of source with saturation and luminosity of destination
        HSL_SATURATION, // saturation of source with hue and luminosity of destination
        HSL_COLOR,      // hue and saturation of source with luminosity of destination
        HSL_LUMINOSITY  // luminosity of source with hue and saturation of destination
    }
}
