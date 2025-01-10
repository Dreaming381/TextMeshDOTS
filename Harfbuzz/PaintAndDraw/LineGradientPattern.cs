using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz
{
    //To-Do: implement this via matrix rotation. Would avoid  "horizontal" and "vertical" check for every pixel
    public struct LineGradient : IPattern
    {
        NativeArray<ColorStop> m_colorStops;
        int m_colorStopCount;
        PaintExtend wrapMode;
        float x01;
        float y01;
        float x0;
        float y0;
        public float p0p2Slope;
        public float p0p2YIntercept;
        public bool horizontal;
        public bool vertical;

        public bool isValid;
        public LineGradient(float x0, float y0, float x1, float y1, float x2, float y2, PaintExtend paintExtend)
        {
            if (Hint.Unlikely((x0 == x1 && y0 == y1) || (x0 == x2 && y0 == y2)))
                isValid = false; //points idential, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr            

            this.x0 = x0;
            this.y0 = y0;

            var x02 = x2 - x0;
            var y02 = y2 - y0;
            x01 = x1 - x0;
            y01 = y1 - y0;

            double det = PaintUtils.cross(x01, y01, x02, y02);
            if (Hint.Unlikely(math.abs(det) < PaintUtils.Epsilon))
                isValid = false; //lines are parallel, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr

            horizontal = x02 == 0 ? true : false;
            vertical = y02 == 0 ? true : false;

            p0p2Slope = y02 / x02;
            p0p2YIntercept = y2 + (x2 * -p0p2Slope);

            m_colorStops = default;
            m_colorStopCount = 0;
            wrapMode = paintExtend;
            isValid = true;
        }

        public void InitializeColorLine(ColorLine colorLine)
        {
            m_colorStopCount = colorLine.GetColorStops(0, out NativeArray<ColorStop> colorStops);
            m_colorStops = colorStops;
        }
        /// <summary> Method to turn design space coordinate into UV. Line gradient only has U </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float GetU(float x, float y)
        {
            if (horizontal)
                return (x - x0) / x01;
            else if (vertical)
                return (y - y0) / y01;

            var xOnP0P2Slope = (y - p0p2YIntercept) / p0p2Slope;
            return (x - xOnP0P2Slope) / x01;
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
            PaintUtils.ApplyWrapMode(ref u, wrapMode);
            return PaintUtils.SampleGradient(m_colorStops, m_colorStopCount, u);
        }
    }
}
