namespace TextMeshDOTS.Clipper2AoS
{
    public struct HorzJoin
    {
        public int op1;
        public int op2;
        public HorzJoin(int ltor, int rtol)
        {
            op1 = ltor;
            op2 = rtol;
        }
    };

} //namespace