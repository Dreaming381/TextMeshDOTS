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
        PaintExtend wrapMode;
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
        public SweepGradient(float x0, float y0, float startAngle, float endAngle, PaintExtend paintExtend)
        {
            if (Hint.Unlikely(startAngle == endAngle && (paintExtend == PaintExtend.REPEAT || paintExtend == PaintExtend.REFLECT)))
                isValid = false; //poins idential, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr            

            this.x0 = x0;
            this.y0 = y0;
            this.startAngle = startAngle;
            this.endAngle = endAngle;
            sectorRange = (endAngle - startAngle);

            startAngleScaled = default;
            endAngleScaled = default;
            m_colorStops = default;
            minStop = default;
            maxStop = default;
            m_colorStopCount = 0;
            wrapMode = paintExtend;
            isValid = true;
        }

        public void InitializeColorLine(ColorLine colorLine)
        {
            m_colorStopCount = colorLine.GetColorStops(0, out NativeArray<ColorStop> colorStops);
            m_colorStops = colorStops;

            minStop = colorStops[0].offset;
            maxStop = colorStops[m_colorStopCount - 1].offset;
            sectorRange = (endAngle - startAngle) / (maxStop - minStop);
            //Debug.Log($"Angle Range: {math.degrees(sectorRange)} {(maxStop - minStop)}");

            startAngleScaled = startAngle + sectorRange * minStop;
            endAngleScaled = startAngle + sectorRange * maxStop;

            //Debug.Log($"startAngle: {math.degrees(startAngle)} scaled: {math.degrees(startAngleScaled)}");
            //Debug.Log($"endAngle: {math.degrees(endAngle)} scaled: {math.degrees(endAngleScaled)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColorARGB GetColor(float x, float y)
        {
            //To-Do: startAngle > endAngle should reverse gradient
            var angle = math.atan2(y - y0, x - x0);   //returns angle from 0 to 2PI 
            angle = PaintUtils.WrapAroundLimit(angle, math.PI2);
            var t = (angle / (endAngleScaled - startAngleScaled)) - startAngle / (endAngle - startAngle);
            PaintUtils.ApplySweepWrapMode(ref t, minStop, maxStop, wrapMode);
            return PaintUtils.SampleGradient(m_colorStops, m_colorStopCount, t);
        }
        public float Interpolate(float v1, float v2, float f)
        {
            return v1 + f * (v2 - v1);
        }
    }
}
