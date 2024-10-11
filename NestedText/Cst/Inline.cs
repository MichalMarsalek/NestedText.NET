using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText.Cst;

internal abstract class Inline : Node
{
    public InlineLine Line { get; set; }
    public string LeadingWhiteSpace { get; set; }
    public string Suffix { get; set; } = "";
    public int ValueStart { get; set; }
    public int ValueEnd { get; set; }
    public string SuffixNonWhiteSpace => Suffix.TrimStart();
    public int SuffixNonWhiteSpaceStart => ValueEnd + Suffix.Length - SuffixNonWhiteSpace.Length;
    public abstract JsonNode ToJsonNode();
    public abstract StringBuilder AppendValue(StringBuilder builder);
    internal abstract Inline Transform(NestedTextSerializerOptions options, bool isFirst);
    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendValue(builder.Append(LeadingWhiteSpace)).Append(Suffix);
    protected internal ParsingError ToError(string message, int? offset = null)
        => Line.ToError(message, offset);
    public override List<CommentLine> CalcComments() => [];
}
internal class InlineString : Inline
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> CalcErrors()
        => Suffix.IsWhiteSpace() ? [] : [ToError($"Extra character after string value. Expected ',' or '}}', found '{SuffixNonWhiteSpace[0]}'.", SuffixNonWhiteSpaceStart)];
    public override int CalcDepth() => 0;

    internal override Inline Transform(NestedTextSerializerOptions options, bool isFirst)
    {
        return new InlineString
        {
            LeadingWhiteSpace = new string(' ', isFirst ? 0 : 1),
            Value = Value,
            Suffix = Suffix.IsWhiteSpace() ? "" : Suffix
        };
    }

    public override StringBuilder AppendValue(StringBuilder builder)
        => builder.Append(Value);

    public override JsonNode ToJsonNode()
        => JsonValue.Create(Value);
}
internal class InlineList : Inline
{
    public required IEnumerable<Inline> Values { get; set; }
    public bool Unterminated { get; set; }
    public override int CalcDepth() => 1 + Values.Max(x => x.Depth, -1);
    public override IEnumerable<ParsingError> CalcErrors()
    {
        foreach (var item in Values)
        {
            foreach (var error in item.Errors)
            {
                // This is slightly hacky.
                // Inline nodes assume they are inside a dictionary by default, we need to adjust the error messages here, since they are not.
                yield return new ParsingError(error.LineNumber, error.ColumnNumber, error.Message.Replace("',' or '}'", "',' or ']'"));
            }
        }
        if (Unterminated)
        {
            var message = "Unterminated inline list.";
            message += ValueEnd >= Line.RawLine.Length ? " Line ended without closing delimiter."
                : ValueStart + 1 == ValueEnd ? " Expected value."
                : $" Expected ',' or ']', found '{Line.RawLine[ValueEnd]}'.";
            yield return ToError(message, ValueEnd);
        }
        if (!Suffix.IsWhiteSpace())
        {
            var suffix = SuffixNonWhiteSpace;
            if (ValueStart == Line.Indentation)
            {
                yield return ToError($"Extra character{(SuffixNonWhiteSpace.Length > 1 ? "s" : "")} after closing delimiter: '{SuffixNonWhiteSpace}'.", SuffixNonWhiteSpaceStart);
            }
            else
            {
                yield return ToError($"Extra character after closing delimiter. Expected ':', found '{SuffixNonWhiteSpace[0]}'.", SuffixNonWhiteSpaceStart);
            }
        }
    }

    internal override Inline Transform(NestedTextSerializerOptions options, bool isFirst)
    {
        return new InlineList
        {
            LeadingWhiteSpace = new string(' ', isFirst ? 0 : 1),
            Values = Values.Select((x,i) => x.Transform(options, i == 0)),
            Unterminated = Unterminated,
            Suffix = Suffix.IsWhiteSpace() ? "" : Suffix
        };
    }

    public override StringBuilder AppendValue(StringBuilder builder)
    {
        builder.Append("[");
        var i = 0;
        foreach (var v in Values)
        {
            if (i++ > 0) builder.Append(",");
            v.Append(builder);
        }
        return Unterminated ? builder : builder.Append("]");
    }

    public override JsonNode ToJsonNode()
        => new JsonArray(Values.Select(x => x.ToJsonNode()).ToArray());
}
internal class InlineDictionary : Inline
{
    public required IEnumerable<List<Inline>> KeyValues { get; set; }
    public bool Unterminated { get; set; }
    public override int CalcDepth() => 1 + KeyValues.Where(x => x.Count == 2).Max(x => x[1].Depth, -1);
    public override IEnumerable<ParsingError> CalcErrors()
    {
        var keys = new HashSet<string>();
        foreach (var item in KeyValues)
        {
            var keyErrorYielded = false;
            if (item[0] is InlineString stringNode)
            {
                if (!keys.Add(stringNode.Value))
                {
                    yield return ToError("Duplicate dictionary key.", stringNode.ValueStart);
                }
                if (!stringNode.Suffix.IsWhiteSpace())
                {
                    keyErrorYielded = true;
                    yield return ToError($"Extra character after string value. Expected ':', found '{stringNode.SuffixNonWhiteSpace[0]}'.", stringNode.SuffixNonWhiteSpaceStart);
                }
            }
            else
            {
                yield return ToError($"Key must be an inline string. Expected ':', found '{Line.RawLine[item[0].ValueStart]}'.", item[0].ValueStart);
            }
            if (item.Count != 2)
            {
                var message = $"Key value pair expected, but found {item.Count} colon separated value{(item.Count > 1 ? "s" : "")}.";
                int offset;
                if (item.Count == 1)
                {
                    offset = item[0].ValueEnd;
                    if (offset >= Line.RawLine.Length)
                    {
                        message += " Line ended without closing delimiter.";
                    }
                    else
                    {
                        message += $" Expected ':', found '{Line.RawLine[offset]}'.";
                    }
                }
                else
                {
                    offset = item[2].ValueStart;
                }
                yield return ToError(message, offset);
            }

            var xi = 0;
            foreach (var x in item)
            {
                if (xi > 0 || !keyErrorYielded)
                {
                    foreach (var error in x.Errors)
                    {
                        yield return error;
                    }
                }
                xi++;
            }
        }
        if (Unterminated)
        {
            var message = "Unterminated inline dictionary.";
            message += ValueEnd >= Line.RawLine.Length ? " Line ended without closing delimiter."
                : ValueStart + 1 == ValueEnd ? " Expected value."
                : $" Expected ',' or '}}', found '{Line.RawLine[ValueEnd]}'.";
            yield return ToError(message, ValueEnd);
        }
        if (!Suffix.IsWhiteSpace())
        {
            if (ValueStart == Line.Indentation)
            {
                yield return ToError($"Extra character{(SuffixNonWhiteSpace.Length > 1 ? "s" : "")} after closing delimiter: '{SuffixNonWhiteSpace}'.", SuffixNonWhiteSpaceStart);
            }
            else
            {
                yield return ToError($"Extra character after closing delimiter. Expected ':', found '{SuffixNonWhiteSpace[0]}'.", SuffixNonWhiteSpaceStart);
            }
        }
    }

    internal override Inline Transform(NestedTextSerializerOptions options, bool isFirst)
    {
        return new InlineDictionary
        {
            LeadingWhiteSpace = new string(' ', isFirst ? 0 : 1),
            KeyValues = KeyValues.Select((kv, i) => kv.Select((x,j) => x.Transform(options, i == 0 && j == 0)).ToList()),
            Unterminated = Unterminated,
            Suffix = Suffix.IsWhiteSpace() ? "" : Suffix
        };
    }

    public override StringBuilder AppendValue(StringBuilder builder)
    {
        builder.Append("{");
        var i = 0;
        foreach (var v in KeyValues)
        {
            if (i++ > 0) builder.Append(",");
            v[0].Append(builder);
            foreach (var x in v.Skip(1))
            {
                builder.Append(":");
                x.Append(builder);
            }
        }
        return Unterminated ? builder : builder.Append("}");
    }

    public override JsonNode ToJsonNode()
        => new JsonObject(KeyValues.Select(kv => new KeyValuePair<string, JsonNode?>(
            (kv[0] as InlineString)?.Value ?? "",
            kv.Count > 1 ? kv[1].ToJsonNode() : JsonValue.Create("")
        )));
}