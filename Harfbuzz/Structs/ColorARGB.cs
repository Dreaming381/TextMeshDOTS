using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ColorARGB : IEquatable<ColorARGB>
    {
        [FieldOffset(0)]
        private int argb;

        [FieldOffset(0)]
        public byte a;

        [FieldOffset(1)]
        public byte r;

        [FieldOffset(2)]
        public byte g;

        [FieldOffset(3)]
        public byte b;

        public byte this[int index]
        {
            get
            {
                return index switch
                {
                    0 => a,
                    1 => r,
                    2 => g,
                    3 => b,
                    _ => throw new IndexOutOfRangeException("Invalid ColorBGRA index(" + index + ")!"),
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        a = value;
                        break;
                    case 1:
                        r = value;
                        break;
                    case 2:
                        g = value;
                        break;
                    case 3:
                        b = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Invalid ColorBGRA index(" + index + ")!");
                }
            }
        }

        public ColorARGB(byte a, byte r, byte g, byte b)
        {
            argb = 0;
            this.a = a;
            this.r = r;
            this.g = g;
            this.b = b; 
        }
        public static implicit operator uint(ColorARGB c)
        {
            return (((uint)c.b & 0xFF) << 24) | (((uint)c.g & 0xFF) << 16) | (((uint)c.r & 0xFF) << 8) | ((uint)c.a & 0xFF);
        }

        public static implicit operator ColorARGB(Color32 c)
        {
            return new ColorARGB(c.a, c.r, c.g, c.b);
        }

        public static implicit operator Color32(ColorARGB c)
        {
            return new Color32(c.r, c.g, c.b, c.a);
        }
        public static implicit operator ColorARGB(Color c)
        {
            return new ColorARGB((byte)Mathf.Round(Mathf.Clamp01(c.a) * 255f), (byte)Mathf.Round(Mathf.Clamp01(c.r) * 255f), (byte)Mathf.Round(Mathf.Clamp01(c.g) * 255f), (byte)Mathf.Round(Mathf.Clamp01(c.b) * 255f));
        }

        public static implicit operator Color(ColorARGB c)
        {
            return new Color((float)(int)c.r / 255f, (float)(int)c.g / 255f, (float)(int)c.b / 255f, (float)(int)c.a / 255f);
        }

        public static ColorARGB Lerp(ColorARGB a, ColorARGB b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ColorARGB((byte)((float)(int)a.a + (float)(b.a - a.a) * t), (byte)((float)(int)a.r + (float)(b.r - a.r) * t), (byte)((float)(int)a.g + (float)(b.g - a.g) * t), (byte)((float)(int)a.b + (float)(b.b - a.b) * t));
        }

        public static ColorARGB LerpUnclamped(ColorARGB a, ColorARGB b, float t)
        {
            return new ColorARGB((byte)((float)(int)a.a + (float)(b.a - a.a) * t), (byte)((float)(int)a.r + (float)(b.r - a.r) * t), (byte)((float)(int)a.g + (float)(b.g - a.g) * t), (byte)((float)(int)a.b + (float)(b.b - a.b) * t));
        }

        public override int GetHashCode()
        {
            return argb.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (other is ColorARGB other2)
            {
                return Equals(other2);
            }

            return false;
        }

        public bool Equals(ColorARGB other)
        {
            return argb == other.argb;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"ARGB {a} {r} {g} {b} ";
        }
    }
}
