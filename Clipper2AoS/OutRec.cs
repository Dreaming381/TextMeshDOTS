namespace TextMeshDOTS.Clipper2AoS
{
    // OutRec: path data structure for clipping solutions
    public struct OutRec
    {
        public int idx;
        public int splitStart;
        public int owner;
        public int frontEdge;
        public int backEdge;
        public int pts;
        public int polypath;
        public Rect64 bounds;
        public bool isOpen;
    };

} //namespace