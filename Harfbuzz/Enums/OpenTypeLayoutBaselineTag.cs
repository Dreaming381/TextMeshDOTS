using UnityEngine;

namespace HarfBuzz
{
    public enum OpenTypeLayoutBaselineTag
    {
        Roman = ('r' << 24) | ('o' << 16) | ('m' << 8) | 'n', //better would be HB.HB_TAG('c', 'p', 'c', 't'), but this does not work in C Sharp,
        Hanging = ('h' << 24) | ('a' << 16) | ('n' << 8) | 'g',
        IdeoFaceBottomOrLeft = ('i' << 24) | ('c' << 16) | ('f' << 8) | 'b',
        IdeoFaceTopOrRight = ('i' << 24) | ('c' << 16) | ('f' << 8) | 't',
        IdeoEmboxBottomOrLeft = ('i' << 24) | ('d' << 16) | ('e' << 8) | 'o',
        IdeoEmboxTopOrRight = ('i' << 24) | ('d' << 16) | ('t' << 8) | 'p',
        IdeoEmboxCentral = ('I' << 24) | ('d' << 16) | ('t' << 8) | 'p',
        Math = ('m' << 24) | ('a' << 16) | ('t' << 8) | 'h',
    }
}
