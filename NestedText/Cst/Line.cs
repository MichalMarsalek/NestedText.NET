using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText.Cst;

internal abstract class Line : Node
{
    public int Indentation { get; set; }
    public int LineNumber { get; set; }
    public Block Nested { get; set; } = new Block([]);
    public override IEnumerable<ParsingError> Errors => Nested.Errors;
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
        => Nested.Lines.Where(line => line is not BlankLine && line is not CommentLine).Select(x => x.ToError("Unexpected indentation.", expectedIndentation));
    public bool NestedHasValue => Nested.Kind != null;
}

internal class IgnoredLine : Line
{
    internal required string Content { get; set; }
    public override IEnumerable<ParsingError> Errors => Enumerable.Empty<ParsingError>();

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        return this;
    }

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder;
}

internal class BlankLine : IgnoredLine { }
internal class CommentLine : IgnoredLine
{
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append("#" + Content);
}
internal class ErrorLine : IgnoredLine
{
    public required string Message { get; set; }
}

internal class StringLine : Line
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> Errors => AllNestedToErrors(Indentation);

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Value == "" ? ">" : "> ").Append(Value);

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal class TaglessStringLine : Line
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> Errors => AllNestedToErrors(Indentation);

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Value);

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal class ListItemLine : Line
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

    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            if (RestOfLine != null && Nested.Kind != null && Nested.Kind != BlockKind.TaglessString)
            {
                return AllNestedToErrors(Indentation);
            }
            return Nested.Errors;
        }
    }

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal abstract class DictionaryLine : Line { }

internal class DictionaryItemLine : DictionaryLine
{
    public required string Key { get; set; }
    public string? RestOfLine { get; set; }

    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            if (RestOfLine != null && Nested.Kind != null && Nested.Kind != BlockKind.TaglessString)
            {
                return AllNestedToErrors(Indentation);
            }
            return Nested.Errors;
        }
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
        => builder.Append(Key).Append(RestOfLine == null ? ":" : ": " + RestOfLine);

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal class KeyItemLine : DictionaryLine
{
    public required string Key { set; get; }
    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Key == "" ? ":" : ": ").Append(Key);
}

internal abstract class Inline : Line
{
    public int TrailingSpaces { get; set; } = 0;
    public abstract StringBuilder AppendValue(StringBuilder builder);
    public StringBuilder AppendNested(StringBuilder builder)
        => AppendValue(builder.Append(new string(' ', Indentation))).Append(new string(' ', TrailingSpaces));
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => AppendValue(builder).Append(new string(' ', TrailingSpaces));
}
internal class InlineString : Inline
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> Errors => Enumerable.Empty<ParsingError>();

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }

    public override StringBuilder AppendValue(StringBuilder builder)
        => builder.Append(Value);
}
internal class InlineList : Inline
{
    public required IEnumerable<Inline> Values { get; set; }
    public override IEnumerable<ParsingError> Errors => Values.SelectMany(x => x.Errors);

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }

    public override StringBuilder AppendValue(StringBuilder builder)
    {
        builder.Append("[");
        var i = 0;
        foreach (var v in Values)
        {
            if (i++ > 0) builder.Append(",");
            v.AppendNested(builder);
        }
        return builder.Append("]");
    }
}
internal class InlineDictionary : Inline
{
    public required IEnumerable<KeyValuePair<InlineString, Inline>> KeyValues { get; set; }
    public override IEnumerable<ParsingError> Errors => KeyValues.SelectMany(x => x.Value.Errors);

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }

    public override StringBuilder AppendValue(StringBuilder builder)
    {
        builder.Append("{");
        var i = 0;
        foreach (var v in KeyValues)
        {
            if (i++ > 0) builder.Append(",");
            v.Key.AppendNested(builder);
            builder.Append(":");
            v.Value.AppendNested(builder);
        }
        return builder.Append("}");
    }
}

internal class InlineError : Inline
{
    public required ParsingError Error { get; set; }
    public required string Content { get; set; }
    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            yield return Error;
        }
    }

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }

    public override StringBuilder AppendValue(StringBuilder builder)
        => builder.Append(Content);
}