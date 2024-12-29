using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TextMeshDOTS;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS.HarfBuzz.SDF
{
    public struct PaintData : IDisposable
    {
        public int width;
        public int height;
        public DrawDelegates drawDelegates;
        public DrawData clipGlyph;
        public BBox clipRect;
        public FixedStack512Bytes<float2x3> transformStack;
        //public FixedStack512Bytes<CairoTransform> transformStack;
        public uint color;
        public NativeArray<Color32> textureData;

        public HB_PAINT_IMAGE_FORMAT imageFormat;
        public Blob imageBlob;

        public PaintData(DrawDelegates drawDelegates, NativeArray<Color32> textureData, int width, int height, int edgeCapacity, int contourCapacity, Allocator allocator)
        {
            this.drawDelegates = drawDelegates;
            clipGlyph = new DrawData(edgeCapacity, contourCapacity, allocator);
            imageFormat = default;
            imageBlob = default;
            clipRect = BBox.Empty;
            transformStack = new();
            transformStack.Add(new float2x3
            {
                c0 = new float2(1, 0),
                c1 = new float2(0, 1),
                c2 = new float2(0, 0)
            });

            color = default;
            this.textureData = textureData;
            this.width = width;
            this.height = height;
        }
        public void Clear()
        {
            clipGlyph.Clear();
            imageFormat = default;
            imageBlob = default;
            clipRect = BBox.Empty;
            transformStack.Clear();
            transformStack.Add(new float2x3
            {
                c0 = new float2(1, 0),
                c1 = new float2(0, 1),
                c2 = new float2(0, 0)
            });
        }
        public void Dispose()
        {
            imageBlob.Dispose();
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ColorBGRA : IEquatable<ColorBGRA>
    {
        [FieldOffset(0)]
        private int bgra;

        [FieldOffset(0)]
        public byte b;

        [FieldOffset(1)]
        public byte g;

        [FieldOffset(2)]
        public byte r;

        [FieldOffset(3)]
        public byte a;

        public byte this[int index]
        {
            get
            {
                return index switch
                {
                    0 => b,
                    1 => g,
                    2 => r,
                    3 => a,
                    _ => throw new IndexOutOfRangeException("Invalid ColorBGRA index(" + index + ")!"),
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        b = value;
                        break;
                    case 1:
                        g = value;
                        break;
                    case 2:
                        r = value;
                        break;
                    case 3:
                        a = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Invalid ColorBGRA index(" + index + ")!");
                }
            }
        }

        public ColorBGRA(byte b, byte g, byte r, byte a)
        {
            bgra = 0;
            this.b = b;
            this.g = g;
            this.r = r;
            this.a = a;
        }

        public static implicit operator ColorBGRA(Color32 c)
        {
            return new ColorBGRA(c.b, c.g, c.r, c.a);
        }

        public static implicit operator Color32(ColorBGRA c)
        {
            return new Color32(c.r, c.g, c.b, c.a);
        }

        public static ColorBGRA Lerp(ColorBGRA a, ColorBGRA b, float t)
        {
            t = Mathf.Clamp01(t);
            return new ColorBGRA((byte)((float)(int)a.b + (float)(b.b - a.b) * t), (byte)((float)(int)a.g + (float)(b.g - a.g) * t), (byte)((float)(int)a.r + (float)(b.r - a.r) * t), (byte)((float)(int)a.a + (float)(b.a - a.a) * t));
        }

        public static ColorBGRA LerpUnclamped(ColorBGRA a, ColorBGRA b, float t)
        {
            return new ColorBGRA((byte)((float)(int)a.b + (float)(b.b - a.b) * t), (byte)((float)(int)a.g + (float)(b.g - a.g) * t), (byte)((float)(int)a.r + (float)(b.r - a.r) * t), (byte)((float)(int)a.a + (float)(b.a - a.a) * t));
        }

        public override int GetHashCode()
        {
            return bgra.GetHashCode();
        }

        public override bool Equals(object other)
        {
            if (other is ColorBGRA other2)
            {
                return Equals(other2);
            }

            return false;
        }

        public bool Equals(ColorBGRA other)
        {
            return bgra == other.bgra;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"BGRA {b} {g} {r} {a} ";
        }
    }
}