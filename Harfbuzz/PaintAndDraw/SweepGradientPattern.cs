using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz
{
    public struct SweepGradient : IPattern
    {
        //https://github.com/foo123/Gradient/blob/80e362bea2cb7deb3ab4c2125bf6fa49a726e4be/README.md
        NativeArray<ColorStop> m_colorStops;
        int m_colorStopCount;
        PaintExtend paintExtend;
        float x0;
        float y0;
        float startAngle;
        float endAngle;
        float sectorRange;
        float startAngleScaled;
        float endAngleScaled;
        float minStop;
        float maxStop;
        public bool isValid;
        public SweepGradient(float x0, float y0, float startAngle, float endAngle, PaintExtend paintExtend, float2x3 transform)
        {
            if (Hint.Unlikely(startAngle == endAngle && (paintExtend == PaintExtend.REPEAT || paintExtend == PaintExtend.REFLECT)))
                isValid = false; //points idential, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr            

            // the object to which the gradient will be applied needs to be transformed
            // prior to rasterization, however color is sampled DURING rasterization,
            // which is why we need to transform the gradient definition.
            // this will also accomodate additional transformations of the gradient
            // relative to the object (which are possible according to the COLRv1 spec)
            var c0 = PaintUtils.mul(transform, new float2(x0, y0));
            var scale = (transform.c0.x + transform.c1.y) / 2;

            this.x0 = c0.x;
            this.y0 = c0.y;
            this.startAngle = startAngle;
            this.endAngle = endAngle;
            sectorRange = (endAngle - startAngle);

            startAngleScaled = default;
            endAngleScaled = default;
            m_colorStops = default;
            minStop = default;
            maxStop = default;
            m_colorStopCount = 0;
            this.paintExtend = paintExtend;
            isValid = true;
        }

        public void InitializeColorLine(ColorLine colorLine)
        {
            m_colorStopCount = colorLine.GetColorStops(0, out NativeArray<ColorStop> colorStops);
            m_colorStops = colorStops;

            minStop = colorStops[0].offset;
            maxStop = colorStops[m_colorStopCount - 1].offset;
            sectorRange = (endAngle - startAngle) / (maxStop - minStop);

            startAngleScaled = startAngle + sectorRange * minStop;
            endAngleScaled = startAngle + sectorRange * maxStop;
        }

        /// <summary>
        /// For a given vertex (/object space pixel) of the rendered glyph, this method calculates 
        /// the UV coordinates that a texture of the color gradient would have. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColorARGB GetColor(float x, float y)
        {
            var angle = math.atan2(y - y0, x - x0);   //returns angle from 0 to 2PI 
            angle = PaintUtils.WrapAroundLimit(angle, math.PI2);
            var t = (angle / (endAngleScaled - startAngleScaled)) - startAngle / (endAngle - startAngle);
            PaintUtils.ApplySweepWrapMode(ref t, minStop, maxStop, paintExtend);
            return PaintUtils.SampleGradient(m_colorStops, m_colorStopCount, t);
        }
        public float Interpolate(float v1, float v2, float f)
        {
            return v1 + f * (v2 - v1);
        }
    }
}
