namespace TextMeshDOTS.Clipper2AoS
{
    // OutRec: path data structure for clipping solutions
    public struct OutRec
    {
        //public int outPtCount; //count is not correct and not needed
        public int owner;
        public int frontEdge;
        public int backEdge;
        public int pts;
        public int polypath; //store here index of polyTree node
        public Rect64 bounds;
        public bool isOpen;
        public int splits;
        public int recursiveSplit;
    };

} //namespace