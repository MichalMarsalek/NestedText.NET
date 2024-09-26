namespace NestedText;

public class NestedTextSerializerOptions
{
    /// <summary>
    /// The amount of spaces that gets added with each indented block.
    /// </summary>
    public int Indentation { get; set; } = 2;

    /// <summary>
    /// Max depth of a structure to emit as an inline value.
    /// </summary>
    public int? MaxDepthToInline { get; set; } = 1;

    /// <summary>
    /// Max line legth of a line containing an inline value.
    /// </summary>
    public int? MaxLineLengthToInline { get; set; }

    /// <summary>
    /// Whether to emit strings as part of list/dictionary items if possible.
    /// </summary>
    public bool UseRestOfLineStrings { get; set; } = true;

    /// <summary>
    /// Whether to automatically support conversion between null, integers, doubles & booleans and their string representations.
    /// </summary>
    public bool UseDefaultConverters { get; set; } = false;

    /// <summary>
    /// How to interpret an empty document.
    /// This is only relevant in the case of deserializing into a <see cref="System.Text.Json.Nodes.JsonNode"/>.
    /// In other cases, the type deduced from the output type.
    /// </summary>
    public EmptyType EmptyType { get; set; } = EmptyType.Dictionary;

    /// <summary>
    /// Defines which syntactical properties should be left intact when formatting a document.
    /// </summary>
    public FormatOptions FormatOptions { get; set; } = new();
}

public enum EmptyType { String, List, Dictionary }

public class FormatOptions
{
    /// <summary>
    /// Whether to keep indentation when formatting a document.
    /// </summary>
    public bool SkipIndentation { get; set; } = false;

    /// <summary>
    /// Whether to keep inline items alignment.
    /// </summary>
    public bool SkipInlineItemsAlignment { get; set; } = false;

    /// <summary>
    /// Whether to keep inline items on a single line.
    /// </summary>
    public bool SkipInlineToMultiline { get; set; } = false;

    /// <summary>
    /// Whether to keep multiline items on multiple lines.
    /// </summary>
    public bool SkipMultilineToInline { get; set; } = false;

    /// <summary>
    /// Whether to keep strings as rest-of-line or string items.
    /// </summary>
    public bool SkipRestOfLine { get; set; } = false;

    /// <summary>
    /// Whether to keep unterminated documents unterminated.
    /// </summary>
    public bool SkipTermination { get; set; } = false;

    public bool SkipAll
    {
        get => SkipIndentation && SkipInlineItemsAlignment && SkipInlineToMultiline && SkipMultilineToInline && SkipRestOfLine;
        set => SkipIndentation = SkipInlineItemsAlignment = SkipInlineToMultiline = SkipMultilineToInline = SkipRestOfLine = value;
    }
}