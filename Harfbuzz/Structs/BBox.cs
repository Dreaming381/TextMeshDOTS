using System;
using Unity.Mathematics;
using UnityEngine.TextCore;

namespace TextMeshDOTS.HarfBuzz
{
    public struct BBox: IEquatable<BBox>
    {
        public float2 min;
        public float2 max;
        public bool IsValid => math.all(min <= max);
        public float width => max.x - min.x;
        public float height => max.y - min.y;

        /// <summary>  Create an empty, invalid BBox. </summary>
        public static readonly BBox Empty = new BBox { min = float.MaxValue, max = float.MinValue };
        /// <summary>   Create a union of two BBox. </summary>
        public static BBox Union(BBox a, BBox b)
        {
            var min = math.min(a.min, b.min);
            var max = math.max(a.max, b.max);

            return new BBox(min, max);
        }
        /// <summary>   Expands the BBox by the provided padding. </summary>
        public void Expand(int padding)
        {
            min -= padding;
            max += padding;
        }

        public BBox(float2 min, float2 max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>
        /// Bounding box for conic (quadratic) bezier 
        /// <see href="https://iquilezles.org/articles/bezierbbox/">https://iquilezles.org/articles/bezierbbox/</see> 
        /// </summary>
        /// <param name="p0">start point</param>
        /// <param name="p1">controll point</param>
        /// <param name="p2">end point</param>
        public static BBox GetQuadraticBezierBBox(float2 p0, float2 p1, float2 p2)
        {
            var min = math.min(p0, p2);
            var max = math.max(p0, p2);

            if (p1.x < min.x || p1.x > max.x || p1.y < min.y || p1.y > max.y)
            {
                float2 t = math.clamp((p0 - p1) / (p0 - 2.0f * p1 + p2), 0.0f, 1.0f);
                float2 s = 1.0f - t;
                float2 q = s * s * p0 + 2.0f * s * t * p1 + t * t * p2;
                min = math.min(min, q);
                max = math.max(max, q);
            }
            return new BBox(min, max);
        }
        /// <summary>
        /// Bounding box for conic (quadratic) bezier 
        /// <see href="https://iquilezles.org/articles/bezierbbox/">https://iquilezles.org/articles/bezierbbox/</see> 
        /// </summary>
        /// <param name="p0">start point</param>
        /// <param name="p1">controll point 1</param>
        /// <param name="p2">controll point 2</param>
        /// <param name="p3">end point</param>
        public static BBox GetCubicBezierBBox(float2 p0, float2 p1, float2 p2, float2 p3)
        {
            var min = math.min(p0, p3);
            var max = math.max(p0, p3);

            float2 c = -1.0f * p0 + 1.0f * p1;
            float2 b = 1.0f * p0 - 2.0f * p1 + 1.0f * p2;
            float2 a = -1.0f * p0 + 3.0f * p1 - 3.0f * p2 + 1.0f * p3;

            float2 h = b * b - a * c;

            if (math.any(h > float2.zero))
            {
                float2 g = math.sqrt(math.abs(h));
                float2 t1 = math.clamp((-b - g) / a, 0.0f, 1.0f); float2 s1 = 1.0f - t1;
                float2 t2 = math.clamp((-b + g) / a, 0.0f, 1.0f); float2 s2 = 1.0f - t2;
                float2 q1 = s1 * s1 * s1 * p0 + 3.0f * s1 * s1 * t1 * p1 + 3.0f * s1 * t1 * t1 * p2 + t1 * t1 * t1 * p3;
                float2 q2 = s2 * s2 * s2 * p0 + 3.0f * s2 * s2 * t2 * p1 + 3.0f * s2 * t2 * t2 * p2 + t2 * t2 * t2 * p3;

                if (h.x > 0.0)
                {
                    min.x = math.min(min.x, math.min(q1.x, q2.x));
                    max.x = math.max(max.x, math.max(q1.x, q2.x));
                }

                if (h.y > 0.0)
                {
                    min.y = math.min(min.y, math.min(q1.y, q2.y));
                    max.y = math.max(max.y, math.max(q1.y, q2.y));
                }
            }
            return new BBox(min, max);
        }
        public static BBox GetLineBBox(float2 p0, float2 p1)
        {
            var min = math.min(p0, p1);
            var max = math.max(p0, p1);
            return new BBox(min, max);
        }
        //public void Transform(float2x3 transform)
        //{
        //    var bl = min;
        //    var tl = new float2(min.x, max.y);
        //    var tr = max;
        //    var br = new float2(max.x, min.y);

        //    min
        //    var tl = min.y;
        //    var rotatedMin = PaintUtils.mul(transform, glyphRect.min);
        //    return new BBox(min, max);
        //}
        public bool Equals(BBox other)
        {
            return this == other;
        }
        public static bool operator ==(BBox x, BBox y)
        {
            return math.all(x.min == y.min & x.max == y.max);
        }

        public static bool operator !=(BBox x, BBox y)
        {
            return !(x == y);
        }
        public override bool Equals(object obj) => obj is BBox other && Equals(other);
        public override int GetHashCode()
        {
            //return HashCode.Combine(c0, c1);
            int hash = 17;
            hash = hash * 29 + min.GetHashCode();
            hash = hash * 29 + max.GetHashCode();
            return hash;
        }
        public override string ToString()
        {
            return $"x {min.x} y {min.y} width {width} height {height}";
            //return $"min {min} max {max}";
        }
    }
}
