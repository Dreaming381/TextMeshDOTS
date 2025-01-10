using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.Rendering;
using static Unity.Entities.SystemBaseDelegates;

namespace TextMeshDOTS.HarfBuzz
{
    public struct RadialGradient : IPattern
    {
        //https://github.com/foo123/Gradient/blob/80e362bea2cb7deb3ab4c2125bf6fa49a726e4be/README.md
        NativeArray<ColorStop> m_colorStops;
        int m_colorStopCount;
        PaintExtend wrapMode;
        float a;
        float b;
        float c;
        float x0;
        float y0;
        float r0;
        float x1;
        float y1;
        float r1;
        public bool isValid;
        public RadialGradient(float x0, float y0, float r0, float x1, float y1, float r1, PaintExtend paintExtend)
        {
            if (Hint.Unlikely((x0 == x1 && y0 == y1) && (r0 == r1)))
                isValid = false; //poins idential, gradient ill formed, draw nothing https://learn.microsoft.com/en-us/typography/opentype/spec/colr            

            a = r0 * r0 - 2 * r0 * r1 + r1 * r1 - x0 * x0 + 2 * x0 * x1 - x1 * x1 - y0 * y0 + 2 * y0 * y1 - y1 * y1;
            b = -2 * r0 * r0 + 2 * r0 * r1 + 2 * x0 * x0 - 2 * x0 * x1 + 2 * y0 * y0 - 2 * y0 * y1;
            c = -x0 * x0 - y0 * y0 + r0 * r0;
            this.x0 = x0;
            this.y0 = y0;
            this.r0 = r0;
            this.x1 = x1;
            this.y1 = y1;
            this.r1 = r1;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColorARGB GetColor(float x, float y)
        {
            float t;
            var s = PaintUtils.QuadraticRoots(
                a, 
                b - 2 * x * x0 + 2 * x * x1 - 2 * y * y0 + 2 * y * y1, 
                c - x * x + 2 * x * x0 - y * y + 2 * y * y0,
                out float2 roots);

            float px, py, pr;
            px = x - x0; py = y - y0;
            pr = math.sqrt(px * px + py * py);
            if (s == 0)
                return new ColorARGB(255, 255, 255, 255); //outside of cone is not painted.
            else if (s == 2)
            {
                //if (0 <= roots[0] && roots[0] <= 1 && 0 <= roots[1] && roots[1] <= 1) t = math.min(roots[0], roots[1]);
                //else if (0 <= roots[0] && roots[0] <= 1) t = roots[0];
                //else if (0 <= roots[1] && roots[1] <= 1) t = roots[1];
                //else t = math.min(roots[0], roots[1]);
                t = math.max(roots[0], roots[1]);
                if(t < 0 && pr > (r0+r1))
                    return new ColorARGB(255, 255, 255, 255); //outside of cone is not painted.
            }
            else
            {
                t = roots[0];                
            }

            PaintUtils.ApplyWrapMode(ref t, wrapMode);
            return PaintUtils.SampleGradient(m_colorStops, m_colorStopCount, t);            
        }
    }
}
