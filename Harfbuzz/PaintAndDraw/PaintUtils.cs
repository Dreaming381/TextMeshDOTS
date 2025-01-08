using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz
{
    public static class PaintUtils
    {
        public static bool GetGradientDirection(float x0, float y0, float x1, float y1, float x2, float y2, out float2 p3)
        {
            p3 = default;
            if (Hint.Unlikely((x0 == x1 && y0 == y1) || (x0 == x2 && y0 == y2)))
                return false; //points idential, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr

            var x02 = x2 - x0;
            var y02 = y2 - y0;
            var x01 = x1 - x0;
            var y01 = y1 - y0;

            double det = cross(x01, y01, x02, y02);
            if (Hint.Unlikely(math.abs(det) < Epsilon))
                return false; //lines are parallel, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr


            var squaredNorm02 = x02 * x02 + y02 * y02;
            if (squaredNorm02 < Epsilon)
            {
                p3 = new float2(x1, y1);
                return true;
            }
            var k = (x01 * x02 + y01 * y02) / squaredNorm02;
            var x = x1 - k * x02;
            var y = y1 - k * y02;
            p3 = new float2(x, y);
            return true;
        }

        /// <summary> returns angle from -PI to PI </summary>
        public static float Angle(float2 from, float2 to)
        {
            // orientation of angle matches that of the coordinate system.
            // In a left - handed coordinate system, i.e.x pointing right and y down,
            // this will mean you get a positive sign for clockwise angles.
            // If the orientation of the coordinate system is mathematical with y up,
            // you get counterclockwise angles as is the convention in mathematics.
            // Changing the order of the inputs will change the sign,
            // see also https://stackoverflow.com/questions/14066933/direct-way-of-computing-clockwise-angle-between-2-vectors
            var det = cross(from, to);   //# determinant. change order to get other direction
            var dot = math.dot(from, to);


            //var angle = math.atan2(det, dot); //returns angle from -PI to PI (atan2(y, x) = atan2(sin, cos))
            //if (angle < 0) { angle += math.PI2_DBL; }         //returns angle from 0 to 2PI 

            var angle = math.atan2(-det, -dot) + math.PI;   //returns angle from 0 to 2PI 
            angle = WrapAroundLimit(angle, math.PI2);
            return angle;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WrapAroundLimit(float val, float lim)
        {
            return math.clamp(val - math.floor(val / lim) * lim, 0f, lim);
        }
        public static void TransformGlyph(ref DrawData drawData, AffineTransform transform)
        {
            var edges = drawData.edges;
            for (int k = 0, kk = edges.Length; k < kk; k++)
            {
                ref var edge = ref edges.ElementAt(k);
                edge.start_pos = math.transform(transform, new float3(edge.start_pos, 0)).xy;
                edge.end_pos = math.transform(transform, new float3(edge.end_pos, 0)).xy;
                edge.control1 = math.transform(transform, new float3(edge.control1, 0)).xy;
                edge.control2 = math.transform(transform, new float3(edge.control2, 0)).xy;
                //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
            }
            ref var glyphRect = ref drawData.glyphRect;
            glyphRect.min = math.transform(transform, new float3(glyphRect.min, 0)).xy;
            glyphRect.max = math.transform(transform, new float3(glyphRect.max, 0)).xy;
        }
        public static void TransformGlyph(ref DrawData drawData, float2x3 transform)
        {
            var edges = drawData.edges;
            for (int k = 0, kk = edges.Length; k < kk; k++)
            {
                ref var edge = ref edges.ElementAt(k);
                edge.start_pos = mul(transform, edge.start_pos);
                edge.end_pos = mul(transform, edge.end_pos);
                edge.control1 = mul(transform, edge.control1);
                edge.control2 = mul(transform, edge.control2);
                //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
            }
            ref var glyphRect = ref drawData.glyphRect;
            glyphRect.min = mul(transform, glyphRect.min);
            glyphRect.max = mul(transform, glyphRect.max);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x3 mul(float2x3 a, float2x3 b)
        {
            return new float2x3(
                 a.c0.x * b.c0 + a.c0.y * b.c1,
                 a.c1.x * b.c0 + a.c1.y * b.c1,
                 a.c2.x * b.c0 + a.c2.y * b.c1 + b.c2);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 mul(float2x3 a, float2 b)
        {
            return a.c0 * b.x + a.c1 * b.y + a.c2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x3 Translate(float x, float y)
        {
            return new float2x3(
                new float2(1, 0),
                new float2(0, 1),
                new float2(x, y));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x3 Scale(float width, float height)
        {
            return new float2x3(
                new float2(width, 0),
                new float2(0, height),
                new float2(0, 0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x3 Rotate(float angleRadians)
        {
            math.sincos(angleRadians, out float s, out float c);
            return new float2x3(
                new float2(c, s),
                new float2(-s, c),
                new float2(0, 0));
        }
        /// <summary>Finds the magnitude of the cross product of two vectors (if we pretend they're in three dimensions) </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The magnitude of the cross product</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float cross(float2 a, float2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double cross(float ax, float ay, float bx, float by)
        {
            return (ax * by) - (ay * bx);
        }
        public static readonly float Epsilon = 0.000001f;

        public static void ApplyWrapMode(ref float u, PaintExtend paintExtend)
        {
            switch (paintExtend)
            {
                case PaintExtend.REPEAT:
                    u = math.fmod(u, 1.0f);
                    u = u < 0 ? u + 1 : u;
                    break;
                case PaintExtend.PAD:
                    u = math.max(math.min(u, 1.0f), 0.0f);
                    break;
                case PaintExtend.REFLECT:
                    var w = math.fmod(u, 2.0f);
                    if (w > 0)
                    {
                        if (w > 1)
                            u = 1.0f - math.fmod(w, 1.0f);
                        else
                            u = w;
                    }
                    else
                    {
                        if (w < -1)
                            u = math.abs(-1.0f - math.fmod(w, 1.0f));
                        else
                            u = math.abs(w);
                    }
                    break;
            }
            //uv.x = (paintExtend == PaintExtend.REPEAT) ? math.fmod(uv.x, 1.0f) : uv.x; // Wrap
            //uv.x = (paintExtend == PaintExtend.PAD) ? math.max(math.min(uv.x, 1.0f), 0.0f) : uv.x; // Clamp
            //float w = math.fmod(uv.x, 2.0f);
            //uv.x = (paintExtend == PaintExtend.REFLECT) ? (w > 1.0f ? 1.0f - math.fmod(w, 1.0f) : w) : uv.x; // Mirror
        }
        public static void ApplySweepWrapMode(ref float u, float minStop, float maxStop, PaintExtend paintExtend)
        {
            var range = maxStop - minStop;
            switch (paintExtend)
            {
                case PaintExtend.REPEAT:
                    u = math.fmod(u, range);
                    u = u < minStop ? u + range : u;
                    if (u > maxStop)
                        u = minStop + (u - maxStop);
                    if (u < minStop)
                        u = maxStop - (minStop - u);
                    break;
                case PaintExtend.PAD:
                    u = math.max(math.min(u, maxStop), minStop);
                    break;
                case PaintExtend.REFLECT:
                    u = math.fmod(u, 2 * range);
                    if (u < minStop)
                        u = minStop + (minStop - u);
                    if (u > maxStop)
                    {
                        u = maxStop - (u - maxStop);
                        if (u < minStop)
                            u = minStop + (minStop - u);
                    }
                    break;
            }
        }

        public static ColorARGB SampleGradient(NativeArray<ColorStop> stops, int colorStopCount, float u)
        {
            if (stops == null)
                return new ColorARGB(255, 255, 255, 255);

            int stop;
            for (stop = 0; stop < colorStopCount; stop++)
            {
                if (u < stops[stop].offset)
                    break;
            }
            if (stop >= colorStopCount)
            {
                //Debug.Log($"stops too long ( {stop} / {stops.Length - 1}), color {stops[colorStopLength - 1].color} ");
                return stops[colorStopCount - 1].color;
            }
            if (stop == 0)
            {
                //Debug.Log($"stop 0 ({stop} / {colorStopLength - 1}), color {stops[0].color} ");
                return stops[0].color;
            }

            float percentageRange = stops[stop].offset - stops[stop - 1].offset;
            if (percentageRange > Epsilon)
            {
                float blend = (u - stops[stop - 1].offset) / percentageRange;
                //Debug.Log($"blending between ({stop - 1}  and {stop}), color {ColorARGB.LerpUnclamped(stops[stop - 1].color, stops[stop].color, blend)} ");
                return ColorARGB.LerpUnclamped(stops[stop - 1].color, stops[stop].color, blend);
            }
            else
            {
                //Debug.Log($"last stop ({stop} / {colorStopLength - 1}), color {stops[stop - 1].color} ");
                return stops[stop - 1].color;
            }
        }
        public static float2 RayUnitCircleFirstHit(float2 rayStart, float2 rayDir)
        {
            float tca = math.dot(-rayStart, rayDir);
            float d2 = math.dot(rayStart, rayStart) - tca * tca;
            System.Diagnostics.Debug.Assert(d2 <= 1.0f);
            float thc = math.sqrt(1.0f - d2);
            // solutions for t if the ray intersects
            float t0 = tca - thc;
            float t1 = tca + thc;
            float t = math.min(t0, t1);
            if (t < 0.0f)
                t = math.max(t0, t1);
            System.Diagnostics.Debug.Assert(t >= 0);
            return rayStart + rayDir * t;
        }
        public static float RadialAddress(float2 uv, float2 focus)
        {
            //uv = (uv - new float2(0.5f, 0.5f)) * 2.0f;
            //focus = (focus - new Vector2(0.5f, 0.5f)) * 2.0f;
            var pointOnPerimiter = RayUnitCircleFirstHit(focus, math.normalize(uv - focus));

            //return (uv - focus).magnitude / (pointOnPerimiter - focus).magnitude;
            // This is faster
            var diff = pointOnPerimiter - focus;
            if (math.abs(diff.x) > Epsilon)
                return (uv.x - focus.x) / diff.x;
            if (math.abs(diff.y) > Epsilon)
                return (uv.y - focus.y) / diff.y;
            return 0.0f;
        }
        //public static float RadialAddress(float2 uv, float2 focus)
        //{
        //    uv = (uv - new float2(0.5f, 0.5f)) * 2.0f;
        //    //focus = (focus - new Vector2(0.5f, 0.5f)) * 2.0f;
        //    var pointOnPerimiter = RayUnitCircleFirstHit(focus, math.normalize(uv - focus));

        //    //return (uv - focus).magnitude / (pointOnPerimiter - focus).magnitude;
        //    // This is faster
        //    var diff = pointOnPerimiter - focus;
        //    if (math.abs(diff.x) > Epsilon)
        //        return (uv.x - focus.x) / diff.x;
        //    if (math.abs(diff.y) > Epsilon)
        //        return (uv.y - focus.y) / diff.y;
        //    return 0.0f;
        //}
    }
}
