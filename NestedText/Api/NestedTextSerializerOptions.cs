namespace NestedText;

public class NestedTextSerializerOptions
{
    /// <summary>
    /// The amount of spaces that gets added with each indented block.
    /// </summary>
    public int Indentation { get; set; } = 2;

    /// <summary>
    /// Whether tagless string lines should be indented to be aligned with the first line of the value.
    /// </summary>
    public bool AlignTaglessStringLines { get; set; } = false;

    /// <summary>
    /// Max depth of a structure to emit as an inline value.
    /// </summary>
    public int? MaxDepthToInline { get; set; }

    /// <summary>
    /// Max line legth of a line containing an inline value.
    /// </summary>
    public int? MaxLineLengthToInline { get; set; }

    /// <summary>
    /// Whether to use a tagless strings to emit multiline strings.
    /// </summary>
    public bool EmitTaglessStringLines { get; set; } = false;

    /// <summary>
    /// Whether to use a tagless strings when parsing.
    /// </summary>
    public bool ParseTaglessStringLines { get; set; } = true;

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
}

public enum EmptyType { String, List, Dictionary }
