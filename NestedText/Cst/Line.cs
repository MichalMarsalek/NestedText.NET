﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NestedText.Cst;

internal abstract class Line : Node
{
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
    public bool NestedHasValue => Nested.Kind != null;
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
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new BlankLine {
        LineNumber = LineNumber,
        Indentation = indentation,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        Content = Content, 
    };
}
internal class CommentLine : IgnoredLine
{
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append("#" + Content);
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new CommentLine
    {
        LineNumber = LineNumber,
        Indentation = indentation,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        Content = Content,
    };
    public override List<CommentLine> CalcComments() => [this, ..Nested.Comments];
}
internal class ErrorLine : IgnoredLine
{
    public required string Message { get; set; }
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new ErrorLine
    {
        LineNumber = LineNumber,
        Indentation = indentation,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        Content = Content,
        Message = Message
    };
}

internal abstract class ValueLine : Line { }

internal class StringLine : ValueLine
{
    public required string Value { get; set; }
    public override int CalcDepth() => 1;
    public override IEnumerable<ParsingError> CalcErrors() => AllNestedToErrors(Indentation);

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Value == "" ? ">" : "> ").Append(Value);
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new StringLine
    {
        LineNumber = LineNumber,
        Indentation = indentation,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        Value = Value,
    };
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

    public override int CalcDepth() => RestOfLine == null ? Nested.Depth : 1;
    public override IEnumerable<ParsingError> CalcErrors()
    {
        if (RestOfLine != null)
        {
            return AllNestedToErrors(Indentation);
        }
        return Nested.Errors;
    }
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new ListItemLine
    {
        LineNumber = LineNumber,
        Indentation = indentation,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        RestOfLine = RestOfLine,
    };
}

internal abstract class DictionaryLine : ValueLine { }

internal class DictionaryItemLine : DictionaryLine
{
    public required string Key { get; set; }
    public required string KeyTrailingWhiteSpace { get; set; }
    public string? RestOfLine { get; set; }

    public override int CalcDepth() => RestOfLine == null ? Nested.Depth : 1;

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
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new DictionaryItemLine
    {
        LineNumber = LineNumber,
        Indentation = indentation,
        KeyTrailingWhiteSpace = KeyTrailingWhiteSpace,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        RestOfLine = RestOfLine,
        Key = Key
    };
}

internal class KeyItemLine : DictionaryLine
{
    public required string Key { set; get; }
    public override int CalcDepth() => Nested.Depth;
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new KeyItemLine
    {
        Indentation = indentation,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        Key = Key,
    };
    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => builder.Append(Key == "" ? ":" : ": ").Append(Key);
}
internal class InlineLine : Line
{
    public required Inline Inline { set; get; }

    protected internal override StringBuilder AppendLineContent(StringBuilder builder)
        => Inline.Append(builder);
    public override int CalcDepth() => Inline.Depth;
    internal override Line Transform(NestedTextSerializerOptions options, int indentation) => new InlineLine
    {
        Indentation = indentation,
        Nested = Nested.Transform(options, indentation + options.Indentation),
        Inline = options.FormatOptions.SkipInlineItemsAlignment ? Inline : Inline.Transform(options, true),
    };
    public override IEnumerable<ParsingError> CalcErrors() => Inline.Errors;
}