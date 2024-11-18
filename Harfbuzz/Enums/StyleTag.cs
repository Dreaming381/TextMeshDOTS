using UnityEngine;

namespace HarfBuzz
{
    public enum StyleTag
    {
        Italic = ('i' << 24) | ('t' << 16) | ('a' << 8) | 'l', //better would be HB.HB_TAG('i', 't', 'a', 'l'), but this does not work in C Sharp
        OpticalSize = ('o' << 24) | ('o' << 16) | ('s' << 8) | 'z',
        SlantAngle = ('s' << 24) | ('l' << 16) | ('n' << 8) | 't',
        SlantRatio = ('S' << 24) | ('l' << 16) | ('n' << 8) | 't',
        Width = ('w' << 24) | ('d' << 16) | ('t' << 8) | 'h',        
        Weight = ('w' << 24) | ('g' << 16) | ('h' << 8) | 't'
    }
}
