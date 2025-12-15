using System.Collections.Generic;
using Unity.Collections;

namespace TextMeshDOTS.Polybool
{
    public struct CombinedPolySegments
    {
        public NativeList<Segment> combined;
        public bool inverted1;
        public bool inverted2;
        public CombinedPolySegments(NativeList<Segment> combined, bool inverted1, bool inverted2)
        {
            this.combined=combined;
            this.inverted1= inverted1;
            this.inverted2= inverted2;
        }
    }
}