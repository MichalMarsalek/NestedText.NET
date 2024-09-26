﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText.Cst;

internal abstract class Inline : Node
{
    public InlineLine Line { get; set; }
    public int LeadingSpaces { get; set; }
    public string Suffix { get; set; } = "";
    public int ValueStart { get; set; }
    public int ValueEnd { get; set; }
    public int SuffixNonWhiteSpaceStart => ValueEnd + Suffix.Length - Suffix.TrimStart().Length;
    public abstract JsonNode ToJsonNode();
    public abstract StringBuilder AppendValue(StringBuilder builder);
    internal abstract Inline Transform(NestedTextSerializerOptions options, bool isFirst);
    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendValue(builder.Append(new string(' ', LeadingSpaces))).Append(Suffix);
    protected internal ParsingError ToError(string message, int? offset = null)
        => Line.ToError(message, offset);
}
internal class InlineString : Inline
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> Errors
        => Suffix.IsWhiteSpace() ? [] : [ToError("Unexpected characters after a value.", SuffixNonWhiteSpaceStart)];

    internal override Inline Transform(NestedTextSerializerOptions options, bool isFirst)
    {
        return new InlineString
        {
            LeadingSpaces = isFirst ? 0 : 1,
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
    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            foreach (var item in Values)
            {
                foreach (var error in item.Errors)
                {
                    yield return error;
                }
            }
            if (Unterminated)
            {
                yield return ToError("Unterminated inline list.", ValueEnd);
            }
            if (!Suffix.IsWhiteSpace())
            {
                yield return ToError("Unexpected characters after a value.", SuffixNonWhiteSpaceStart);
            }
        }
    }

    internal override Inline Transform(NestedTextSerializerOptions options, bool isFirst)
    {
        return new InlineList
        {
            LeadingSpaces = isFirst ? 0 : 1,
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
    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            var keys = new HashSet<string>();
            foreach (var item in KeyValues)
            {
                if (item.Count != 2)
                {
                    yield return ToError($"Key value pair expected, but found {item.Count} colon separated values.", item.Count < 2 ? item[0].ValueEnd : item[2].ValueStart);
                }
                if (item[0] is InlineString stringNode)
                {
                    if (!keys.Add(stringNode.Value))
                    {
                        yield return ToError("Duplicate dictionary key.", stringNode.ValueStart);
                    }
                }
                else
                {
                    yield return ToError($"Key must be an inline string.", item[0].ValueStart);
                }
                foreach(var x in item)
                {
                    foreach(var error in x.Errors)
                    {
                        yield return error;
                    }
                }
            }
            if (Unterminated)
            {
                yield return ToError("Unterminated inline dictionary.", ValueEnd);
            }
            if (!Suffix.IsWhiteSpace())
            {
                yield return ToError("Unexpected characters after a value.", SuffixNonWhiteSpaceStart);
            }
        }
    }

    internal override Inline Transform(NestedTextSerializerOptions options, bool isFirst)
    {
        return new InlineDictionary
        {
            LeadingSpaces = isFirst ? 0 : 1,
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