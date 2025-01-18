using Unity.Collections;
using Unity.Mathematics;

namespace TextMeshDOTS.HarfBuzz
{
    public static class Blending
    {
        public static ColorARGB SrcOver(ColorARGB source, ColorARGB destination)
        {
            // r = s*sa + (1-sa)*d*da
            var result = ((int4)source * source.a / 255) + (255 - source.a) * ((int4)destination * destination.a / 255) / 255;
            var alpha = source.a + destination.a * (255 - source.a) / 255;
            result[0] = alpha;
            return (ColorARGB)result;
        }
        public static ColorARGB DstOver(ColorARGB source, ColorARGB destination)
        {
            // r = d*da + (1-da)*s*sa
            var result = ((int4)destination * destination.a / 255) + (255 - destination.a) * ((int4)source * source.a / 255) / 255;
            var alpha = destination.a + source.a * (255 - destination.a) / 255;
            result[0] = alpha;
            return (ColorARGB)result;
        }

        //public static ColorARGB SrcIn(ColorARGB source, ColorARGB destination)
        //{
        //    // r = s * da
        //    var result = (int4)source * destination.a / 255;
        //    return (ColorARGB)result;
        //}
        //public static ColorARGB DstIn(ColorARGB source, ColorARGB destination)
        //{
        //    // r = d * sa
        //    var result = (int4)destination * source.a / 255;
        //    return (ColorARGB)result;
        //}
        public static ColorARGB SrcIn(ColorARGB source, ColorARGB destination)
        {
            // r = s *sa * da
            //alpha = sa * da
            var result = (int4)source * source.a / 255 * destination.a / 255;
            var alpha = source.a * destination.a / 255;
            result[0] = alpha;
            return (ColorARGB)result;
        }
        public static ColorARGB DstIn(ColorARGB source, ColorARGB destination)
        {
            // r = s *sa * da
            //alpha = sa * da
            var result = (int4)destination * destination.a / 255 * source.a / 255;
            var alpha = destination.a * source.a / 255;
            result[0] = alpha;
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
            var result = (int4)source / (255 - (int4)destination);
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
        public static void SetWhite(NativeArray<ColorARGB> result)
        {
            for (int i = 0; i < result.Length; i++)
                result[i] = new ColorARGB(255,255,255,255);
        }
    }    
}
