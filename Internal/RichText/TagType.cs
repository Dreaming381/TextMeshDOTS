using UnityEngine;

namespace TextMeshDOTS.RichText
{
    public enum TagType
    {
        Hyperlink,
        Align,
        AllCaps,
        Alpha,
        Bold,
        Br,
        Color,
        CSpace,
        Font,
        FontWeight,
        Italic,
        Indent,
        LineHeight,
        LineIndent,
        Link,
        Lowercase,
        Mark,
        Mspace,
        NoBr,
        NoParse,
        Strikethrough,
        Size,
        SmallCaps,
        Space,
        Sprite,
        Style,
        Subscript,
        Superscript,
        Underline,
        Uppercase,
        Unknown // Not a real tag, used to indicate an error

        //gradient: margin, pos, rotate , width, voffset will not be supported
    }
}
