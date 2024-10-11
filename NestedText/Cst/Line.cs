using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NestedText.Cst;

internal abstract class Line : Node
{
    public string RawLine { get; set; } = "";
    public int Indentation { get; set; }
    public int LineNumber { get; set; }
    public Block Nested { get; set; } = new Block([]);
    public override IEnumerable<ParsingError> CalcErrors() => Nested.Errors;
    abstract protected internal StringBuilder AppendLineContent(StringBuilder builder);
    protected internal override StringBuilder Append(StringBuilder builder)
    {
        builder.Append(new string(' ', Indentation));
        AppendLineContent(builder);
        builder.AppendLine();
        Nested.Append(builder);
        return builder;
    }
    internal ParsingError ToError(string message, int? indentation = null)
        => new ParsingError(LineNumber, (indentation ?? Indentation) + 1, message);
    protected IEnumerable<ParsingError> AllNestedToErrors(int expectedIndentation)
        => Nested.Lines.Where(line => line is not BlankLine && line is not CommentLine)
        .Select(x => x.ToError("Invalid indentation.", expectedIndentation));
    abstract internal Line Transform(NestedTextSerializerOptions options, int indentation);
    public override List<CommentLine> CalcComments() => Nested.Comments;
}

internal abstract class IgnoredLine : Line
{
    public override int CalcDepth() => 0;
    internal required string Content { get; set; }
    public override IEnumerable<ParsingError> CalcErrors() => [];

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Content);
}

internal class BlankLine : IgnoredLine {
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) {
        var fmt = options.FormatOptions;
        if (fmt.Indentation) Indentation = 0;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        return this;
    }
}
internal class CommentLine : IgnoredLine
{
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append("#" + Content);
    internal override Line Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.Indentation) Indentation = indentation;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        return this;
    }
    public override List<CommentLine> CalcComments() => [this, ..Nested.Comments];
}
internal class ErrorLine : IgnoredLine
{
    public required string Message { get; set; }
    internal override Line Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.Indentation) Indentation = indentation;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        return this;
    }
}

internal abstract class ValueLine : Line { }

internal class StringLine : ValueLine
{
    public required string Value { get; set; }
    public override int CalcDepth() => 0;
    public override IEnumerable<ParsingError> CalcErrors() => AllNestedToErrors(Indentation);

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Value == "" ? ">" : "> ").Append(Value);
    internal override Line Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.Indentation) Indentation = indentation;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        return this;
    }
}

internal class ListItemLine : ValueLine
{
    public string? RestOfLine { get; set; }

    public JsonNode ToJsonNode()
    {
        var nested = Nested.ToJsonNode();
        if (RestOfLine == null)
        {
            return nested ?? JsonValue.Create("");
        }
        if (nested == null) return JsonValue.Create(RestOfLine);
        return JsonValue.Create(RestOfLine + nested.GetValue<string>());
    }
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(RestOfLine == null ? "-" : "- " + RestOfLine);

    public override int CalcDepth() => RestOfLine == null ? Nested.Depth : 0;
    public override IEnumerable<ParsingError> CalcErrors()
    {
        if (RestOfLine != null)
        {
            return AllNestedToErrors(Indentation);
        }
        return Nested.Errors;
    }
    internal override Line Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.RestOfLine && RestOfLine == null)
        {
            var restOfLine = Nested.GetRestOfLineValidStringValue();
            if (restOfLine != null)
            {
                RestOfLine = restOfLine;
                Nested = new();
            }
        }
        if (fmt.Indentation) Indentation = indentation;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        return this;
    }
}

internal abstract class DictionaryLine : ValueLine { }

internal class DictionaryItemLine : DictionaryLine
{
    public required string Key { get; set; }
    public required string KeyTrailingWhiteSpace { get; set; }
    public string? RestOfLine { get; set; }

    public override int CalcDepth() => RestOfLine == null ? Nested.Depth : 0;

    public override IEnumerable<ParsingError> CalcErrors()
    {
        if (RestOfLine != null)
        {
            return AllNestedToErrors(Indentation);
        }
        return Nested.Errors;
    }
    public JsonNode ToJsonNode()
    {
        var nested = Nested.ToJsonNode();
        if (RestOfLine == null)
        {
            return nested ?? JsonValue.Create("");
        }
        if (nested == null) return JsonValue.Create(RestOfLine);
        return JsonValue.Create(RestOfLine + nested.GetValue<string>());
    }

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Key).Append(KeyTrailingWhiteSpace).Append(RestOfLine == null ? ":" : ": " + RestOfLine);
    internal override Line Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.RestOfLine && RestOfLine == null)
        {
            var restOfLine = Nested.GetRestOfLineValidStringValue();
            if (restOfLine != null)
            {
                RestOfLine = restOfLine;
                Nested = new();
            }
        }
        if (fmt.Indentation) Indentation = indentation;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        return this;
    }
}

internal class KeyItemLine : DictionaryLine
{
    public required string Key { set; get; }
    public override int CalcDepth() => Nested.Depth;
    internal override Line Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.Indentation) Indentation = indentation;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        return this;
    }
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Key == "" ? ":" : ": ").Append(Key);
}
internal class InlineLine : Line
{
    public required Inline Inline { set; get; }

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => Inline.Append(builder);
    public override int CalcDepth() => Inline.Depth;
    internal override Line Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.Indentation) Indentation = indentation;
        Nested = Nested.Transform(options, indentation + options.Indentation);
        Inline = options.FormatOptions.InlineWhitespace ? Inline.Transform(options, true) : Inline;
        return this;
    }
    public override IEnumerable<ParsingError> CalcErrors() => Inline.Errors;
}