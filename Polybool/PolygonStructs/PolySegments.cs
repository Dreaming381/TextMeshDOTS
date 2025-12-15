using System.Collections.Generic;
using Unity.Collections;

namespace TextMeshDOTS.Polybool
{
    public struct PolySegments
    {        
        public NativeList<Segment> segments;
        public bool inverted;
    }
}