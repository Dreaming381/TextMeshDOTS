using System.IO;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    public interface IPattern
    {
        public ColorARGB GetColor(float x, float y);
    }

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
        public struct LineGradient : IPattern
    {
        NativeArray<ColorStop> m_colorStops;
        int m_colorStopCount;
        PaintExtend wrapMode;
        float x01;
        public float p0p2Slope;
        public float p0p2YIntercept;
        
        public bool isValid;
        public LineGradient(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            if (Hint.Unlikely((x0 == x1 && y0 == y1) || (x0 == x2 && y0 == y2)))
                isValid = false; //poins idential, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr            

            var x02 = x2 - x0;
            var y02 = y2 - y0;
            x01 = x1 - x0;
            var y01 = y1 - y0;

            double det = PaintUtils.cross(x01, y01, x02, y02);
            if (Hint.Unlikely(math.abs(det) < PaintUtils.Epsilon))
                isValid = false; //lines are parallel, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr

            p0p2Slope = y02 / x02;
            p0p2YIntercept = y2 + (x2 * -p0p2Slope);

            m_colorStops = default;
            m_colorStopCount = 0;
            wrapMode = PaintExtend.PAD;
            isValid = true;
        }

        public void InitializeColorLine(ColorLine colorLine)
        {
            wrapMode = colorLine.GetExtend();
            m_colorStopCount = colorLine.GetColorStops(0, out NativeArray<ColorStop> colorStops);
            m_colorStops = colorStops;
            for (int i = 0; i < m_colorStopCount; i++)
            {
                var colorStop = colorStops[i];
                Debug.Log($"{colorStop.offset} {colorStop.color} {colorStop.isForeground} ");
            }
        }
        /// <summary> Method to turn design space coordinate into UV. Line gradient only has U </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float GetU(float x, float y)
        {
            var xOnP0P2Slope = (y - p0p2YIntercept) / p0p2Slope;
            var u = (x - xOnP0P2Slope) / x01;
            return u;
        }

        public ColorARGB GetColor(float u)
        {
            PaintUtils.ApplyWrapMode(ref u, wrapMode);
            return PaintUtils.SampleGradient(m_colorStops, m_colorStopCount, u);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColorARGB GetColor(float x, float y)
        {
            var u = GetU(x, y);
            PaintUtils.ApplyWrapMode(ref u, PaintExtend.REPEAT);
            return PaintUtils.SampleGradient(m_colorStops, m_colorStopCount, u);
        }
    }
    public static class PaintUtils
    {
        public static bool GetGradientDirection(float x0, float y0, float x1, float y1, float x2, float y2, out float2 p3)
        {
            p3 = default;
            if (Hint.Unlikely((x0 ==x1 && y0==y1)|| (x0 == x2 && y0 == y2)))
                return false; //poins idential, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr
            
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
        /// <summary>Creates a left-hand side rotation matrix.</summary>
        /// <param name="angleRadians">The rotation angle, in radians</param>
        /// <returns>The rotation matrix</returns>
        public static float2x3 Rotate(float angleRadians)
        {
            // No SinCos? I hope the compiler optimizes this
            math.sincos(angleRadians, out float s, out float c);
            return new float2x3(
                new float2(c,s),
                new float2(-s,c),
                float2.zero);
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
                    //uv.x = uv.x < 0 ? 1 + math.fmod(uv.x, 1.0f) : math.fmod(uv.x, 1.0f);
                    u = math.fmod(u, 1.0f);
                    u = u < 0 ? u + 1 : u;
                    break;
                case PaintExtend.PAD:
                    u = math.max(math.min(u, 1.0f), 0.0f);
                    break;
                case PaintExtend.REFLECT:
                    var w = math.fmod(u, 2.0f);
                    //uv.x = (w > 1.0f ? 1.0f - math.fmod(w, 1.0f) : w);
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
            uv = (uv - new float2(0.5f, 0.5f)) * 2.0f;
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
    }
    public static class SDFCommon
    {
        /// <summary> Max permitted deviatition of generated lines from original bezier curve. 
        /// Sensible value is fontsize (=atlas pointsize) / 25). Lower values massively hit performance.
        /// </summary>
        public const float MAX_DEVIATION_SPLITTING = 2f;
        public readonly static bool USE_SQUARED_DISTANCES = false;
        public const int DEFAULT_SPREAD = 8;
        public const int MIN_SPREAD = 2;
        public const int MAX_SPREAD = 32;
        public const int MAX_NEWTON_STEPS = 4;
        public const int MAX_NEWTON_DIVISIONS = 4;
        public const int FT_TRIG_SAFE_MSB = 29;
		
		public static void CenterGlyphInGlyphRect(ref DrawData drawData, int width, int height, int padding)
		{
			var edges = drawData.edges;
			var shiftx = -drawData.glyphRect.min.x + ((width - (drawData.glyphRect.width + 2 * padding)) / 2);
            var shifty = -drawData.glyphRect.min.y + ((height -(drawData.glyphRect.height + 2 * padding)) / 2);
            float2 shift = new float2(shiftx, shifty);
            for (int k = 0, kk = edges.Length; k < kk; k++)
            {
                ref var edge = ref edges.ElementAt(k);
                edge.start_pos += shift;
                edge.end_pos += shift;
                edge.control1 += shift;
                edge.control2 += shift;
                //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
            }
            ref var glyphRect = ref drawData.glyphRect;
            glyphRect.min += shift;
			glyphRect.max += shift;
		}
        

        public static void WriteGlyphOutlineToFile(string path, in NativeList<SDFEdge> edges)
        {
            if(edges.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            SDFEdge edge;
            edge = edges[0];
            writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.end_pos.x} {edge.end_pos.y}");              
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, in NativeArray<float> minDistances)
        {
            if (minDistances.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = minDistances.Length; i < end; i++)
            {
                writer.WriteLine($"{minDistances[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, in NativeArray<byte> minDistances)
        {
            if (minDistances.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = minDistances.Length; i < end; i++)
            {
                writer.WriteLine($"{minDistances[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteGlyphOutlineToFile(string path, ref DrawData drawData)
        {
            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            if (contourIDs.Length < 2 || edges.Length == 0)
                return;

            StreamWriter writer = new StreamWriter(path, false);
            SDFEdge edge;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    edge = edges[edgeID];
                    writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
                    //writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y} {edge.control1.x} {edge.control1.y} {edge.end_pos.x} {edge.end_pos.y} {edge.edge_type}");
                }
                writer.WriteLine();
            }
            writer.Close();
        }
        //public static void WriteGlyphOutlineToFile(string path, ref DrawData drawData)
        //      {
        //          var edges = drawData.edges;
        //          var contourIDs= drawData.contourIDs;
        //          if (contourIDs.Length < 2 || edges.Length == 0)
        //              return;

        //          StreamWriter writer = new StreamWriter(path, false);
        //	for (int edgeID = 0, end =drawData.edges.Length ; edgeID <end ; edgeID++) //for each edge
        //	{
        //		var edge = edges[edgeID];
        //		writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y} {edge.end_pos.x} {edge.end_pos.y}");
        //	}
        //	writer.WriteLine();
        //          for (int contourID = 0, end= contourIDs.Length; contourID < end; contourID++) //for each contour
        //          {
        //		var contour = contourIDs[contourID];
        //              writer.WriteLine($"{contour}");               
        //          }
        //          writer.Close();
        //      }
        public static void WriteMinDistancesToFile(string path, in NativeList<SDFDebug> distanceHelper)
        {
            if (distanceHelper.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = distanceHelper.Length; i < end; i++)
            {
                var c = distanceHelper[i];
                writer.WriteLine($"{c.edge.edge_type} {c.x} {c.y} {c.overWrite} {c.pixelWasSet} previous: sign {c.previousPixelValue.sign} cross {c.previousPixelValue.cross} {c.previousPixelValue.distance} current: sign{c.pixelValue.sign} cross {c.pixelValue.cross} {c.pixelValue.distance}");
            }
            writer.WriteLine();
            writer.Close();
        }
    }
    public struct SDFDebug
    {
        public int x;
        public int y;
        public bool pixelWasSet;
        public bool overWrite;
        public SignedDistance previousPixelValue;
        public SignedDistance pixelValue;
        public SDFEdge edge;
    }
}
