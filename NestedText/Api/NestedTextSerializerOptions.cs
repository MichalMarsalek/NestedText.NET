namespace NestedText;

public class NestedTextSerializerOptions
{
    /// <summary>
    /// The amount of spaces that gets added with each indented block.
    /// </summary>
    public int Indentation { get; set; } = 2;

    /// <summary>
    /// Max depth of a structure to emit as an inline value.<br>
    /// Strings, empty lists & empty dictionaries have 0 depth.
    /// Other values have depth equal to 1 + the max of the children's depth.
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
    /// Enables some convenient default behaviour. Namely:
    /// <list type="bullet">
    /// <item>(De)serialization of boolean &amp; number types.</item>
    /// <item>Lower case policy for property names.</item>
    /// <item>Lower case policy for enum members.</item>
    /// <item>(De)serialization of nulls as missing items.</item>
    /// </list>
    /// </summary>
    public bool UseDefaultConventions { get; set; } = false;

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

    /// <summary>
    /// Whether to throw when parsing a document whose last line is not empty.
    /// </summary>
    public bool ThrowOnUnterminated { get; set; } = true;
}

public enum EmptyType { String, List, Dictionary }

public class FormatOptions
{
    /// <summary>
    /// Whether to change indentation when formatting a document.
    /// </summary>
    public bool Indentation { get; set; } = true;

    /// <summary>
    /// Whether to change inline items leading and trailing whitespace.
    /// </summary>
    public bool InlineWhitespace { get; set; } = true;

    /// <summary>
    /// Whether to convert inline items to multiline.
    /// </summary>
    public bool InlineToMultiline { get; set; } = true;

    /// <summary>
    /// Whether to convert multiline items to inline items.
    /// </summary>
    public bool MultilineToInline { get; set; } = true;

    /// <summary>
    /// Whether to ćonvert nested strings to rest-of-line strings.
    /// </summary>
    public bool RestOfLine { get; set; } = true;

    /// <summary>
    /// Whether to terminated unterminated documents.
    /// </summary>
    public bool Termination { get; set; } = true;

    /// <summary>
    /// Whether to transform key items to dictionaryitems.
    /// </summary>
    public bool DictionaryKeys { get; set; } = true;

    public bool SkipAll
    {
        get => !Indentation && !InlineWhitespace && !InlineToMultiline && !MultilineToInline && !RestOfLine && !Termination && !DictionaryKeys;
        set => Indentation = InlineWhitespace = InlineToMultiline = MultilineToInline = RestOfLine = Termination = DictionaryKeys = !value;
    }
}