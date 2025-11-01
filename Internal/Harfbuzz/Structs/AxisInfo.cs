using System.Runtime.InteropServices;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;

namespace TextMeshDOTS.HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AxisInfo
    {
        public uint axisIndex;
        public uint axisTag;
        //public uint tag;
        public NameID nameID;
        public uint flags;
        public float minValue;
        public float defaultValue;
        public float maxValue;
        uint reserved;
        public override string ToString()
        {
            string axis = $"{(char)((axisTag >> 24) & 0xff)} {(char)((axisTag >> 16) & 0xff)} {(char)((axisTag >> 8) & 0xff)} {(char)(axisTag & 0xff)}";
            return $"{axisIndex} {axis} {nameID} {flags} min:{minValue} default:{defaultValue} max:{maxValue}";
            //return $"{axisIndex} {axisTag} {nameID} {flags} min:{minValue} default:{defaultValue} max:{maxValue}";
        }
    }
}
