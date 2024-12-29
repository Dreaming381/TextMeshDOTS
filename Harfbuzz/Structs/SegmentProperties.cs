namespace TextMeshDOTS.HarfBuzz
{
    public struct SegmentProperties
    {
        public Direction direction;
        public Script script;
        public Language language;
        ///*< private >*/
        //void* reserved1;
        //void* reserved2;
        public SegmentProperties(Direction direction, Script script, Language language)
        {
            this.direction = direction;
            this.script = script;
            this.language = language;
        }
        public override string ToString()
        {
            return $"Direction: {direction} Script: {script} Language: {language}";
        }
    }
}
