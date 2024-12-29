namespace TextMeshDOTS.HarfBuzz
{
    public enum MetricTag
    {
        CapHeight = ('c' << 24) | ('p' << 16) | ('h' << 8) | 't', //better would be HB.HB_TAG('c', 'p', 'c', 't'), but this does not work in C Sharp,
        HorizontalAscender = ('h' << 24) | ('a' << 16) | ('s' << 8) | 'c',
        //HorizontalCaretOffset = 1751347046,
        //HorizontalCaretRise = 1751347827,
        //HorizontalCaretRun = 1751347822,
        //HorizontalClippingAscent = 1751346273,
        //HorizontalClippingDescent = 1751346276,
        HorizontalDescender = ('h' << 24) | ('d' << 16) | ('s' << 8) | 'c',
        //HorizontalLineGap = 1751934832,
        //StrikeoutOffset = 1937011311,
        //StrikeoutSize = 1937011315,
        SubScriptEmXOffset = ('s' << 24) | ('b' << 16) | ('x' << 8) | 'o',
        SubScriptEmXSize = ('s' << 24) | ('b' << 16) | ('x' << 8) | 's',
        SubScriptEmYOffset = ('s' << 24) | ('b' << 16) | ('y' << 8) | 'o',
        SubScriptEmYSize = ('s' << 24) | ('b' << 16) | ('y' << 8) | 's',
        SuperScriptEmXOffset = ('s' << 24) | ('p' << 16) | ('x' << 8) | 'o',
        SuperScriptEmXSize = ('s' << 24) | ('p' << 16) | ('x' << 8) | 's',
        SuperScriptEmYOffset = ('s' << 24) | ('p' << 16) | ('y' << 8) | 'o',
        SuperScriptEmYSize = ('s' << 24) | ('p' << 16) | ('y' << 8) | 's',
        //UnderlineOffset = 1970168943,
        //UnderlineSize = 1970168947,
        VerticalAscender = ('v' << 24) | ('a' << 16) | ('s' << 8) | 'c',
        //VerticalCaretOffset = 1986228070,
        //VerticalCaretRise = 1986228851,
        //VerticalCaretRun = 1986228846,
        VerticalDescender = ('v' << 24) | ('a' << 16) | ('d' << 8) | 'c',
        //VerticalLineGap = 1986815856,
        XHeight = ('x' << 24) | ('h' << 16) | ('g' << 8) | 't',
    }
}
