using Codice.Client.BaseCommands;
using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using UnityEngine;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GlyphExtents
    {
        public int x_bearing;
        public int y_bearing;
        public int width;
        public int height;
        public override string ToString()
        {
            return $"x_bearing {x_bearing} y_bearing {y_bearing} {width} {height}";
        }
    }
}

