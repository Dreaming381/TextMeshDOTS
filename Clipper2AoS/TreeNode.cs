namespace TextMeshDOTS.Clipper2AoS
{
    public struct TreeNode
    {
        public int ID;
        public int parentID;
        public int rightID;
        public int childID;
        public TreeNode(int ID)
        {
            this.ID = ID;
            this.parentID = -1;
            this.rightID = -1;
            this.childID = -1;
        }
    };

} //namespace