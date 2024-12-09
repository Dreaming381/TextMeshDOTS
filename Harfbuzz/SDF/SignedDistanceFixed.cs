using UnityEngine;

namespace HarfBuzz.SDF
{
    public struct SignedDistanceFixed
    {
        public int distance;    //FT_16D16
        public int cross;       //FT_16D16
        public sbyte sign;
    }
}
