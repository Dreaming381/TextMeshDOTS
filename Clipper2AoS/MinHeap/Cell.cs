using System;

namespace TextMeshDOTS.Clipper2AoS.MinHeap
{
    public struct Cell : IComparable<Cell>
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float H { get; private set; }
        public float D { get; private set; }
        public float Max { get; private set; }

        public Cell(float x, float y, float h, int distance)
        {
            X = x;
            Y = y;
            H = h;
            D = distance;
            Max = D + H * 1.41421356237f; //*math.sqrt(2);
        }
        public int CompareTo(Cell other)
        {
            if (Max == other.Max)
            {
                return 0;
            }
            else
            {
                if (Max > other.Max)
                    return -1; // this will sort descending 
                else
                    return 1;  //this will sort descending
            }
        }
    }
}
