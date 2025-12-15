namespace TextMeshDOTS.Polybool
{
    public enum FillStatus : byte
    {
        //flip between filled and not-filled using XOR 6, which flips the 2nd and 3rd bit (110)
        Undefined = 0,      //1st bit
        Filled = 2,         //2nd bit 
        NotFilled = 4,      //3d bit
        ToggleMask = 6,     //toggle 2nd and 3d bit
    }
}
