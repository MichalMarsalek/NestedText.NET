using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NestedText.Old;

internal class CstOld
{
    public List<Line> Lines { get; private set; }

    public CstOld(IEnumerable<Line> lines)
    {
        Lines = lines.ToList();
        var lineNumber = 1;
        foreach(var line in lines)
        {
            line.LineNumber = lineNumber++;
        }
    }

    public CstOld Transform(NestedTextSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override string ToString() => Lines.Select(x => x.ToString()).JoinLines() + Environment.NewLine;

    public static CstOld FromJsonNode(JsonNode node, NestedTextSerializerOptions options)
    {
        List<Line> lines = new();
        bool IsValidInlineValue(JsonNode node, int? maxDepth, bool isInsideDictionary)
        {
            if (node is JsonArray _array && _array.Count == 0) return true;
            if (node is JsonObject _obj && _obj.Count == 0) return true;
            if (maxDepth == 0) return false;
            return node switch
            {
                JsonValue value => value.GetValueKind() == JsonValueKind.String && node.GetValue<string>()!.IsValidInlineString(isInsideDictionary),
                JsonArray array => array.All(x => x != null && IsValidInlineValue(x, maxDepth - 1, isInsideDictionary)),
                JsonObject obj => obj.All(x => x.Key.IsValidInlineString(true) && IsValidInlineValue(x.Value, maxDepth - 1, true)),
                _ => false
            };
        }

        void ProcessNode(JsonNode node, int indentation)
        {
            if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                lines.AddRange(value.GetValue<string>()!.GetLines().Select(x => new StringItemLine
                {
                    Indentation = indentation,
                    Value = x
                }));
            }
            else if ((node is JsonArray || node is JsonObject) && IsValidInlineValue(node, options.MaxDepthToInline, false))
            {
                var inlined = EmitInlineValue(node);
                if (options.MaxLineLengthToInline == null || indentation + inlined.Length <= options.MaxLineLengthToInline)
                {
                    lines.Add(new InlineValueLine
                    {
                        Indentation = indentation,
                        Value = inlined
                    });
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var child in array)
                {
                    if (child.GetValueKind() == JsonValueKind.String && child.GetValue<string>().IsValidEndOfLineValue())
                    {
                        lines.Add(new ListItemLine
                        {
                            Indentation = indentation,
                            Value = child.GetValue<string>()
                        });
                    }
                    else
                    {
                        lines.Add(new ListItemLine
                        {
                            Indentation = indentation,
                            Value = ""
                        });
                        ProcessNode(child, indentation + options.Indentation);
                    }
                }
            }
            else if (node is JsonObject obj)
            {
                foreach (var prop in obj)
                {
                    if (prop.Key.IsValidKey())
                    {
                        if (prop.Value!.GetValueKind() == JsonValueKind.String && prop.Value.GetValue<string>().IsValidEndOfLineValue())
                        {
                            lines.Add(new DictionaryItemLine
                            {
                                Indentation = indentation,
                                Key = prop.Key,
                                Value = prop.Value.GetValue<string>()
                            });
                        }
                        else
                        {
                            lines.Add(new DictionaryItemLine
                            {
                                Indentation = indentation,
                                Key = prop.Key,
                                Value = ""
                            });
                            ProcessNode(prop.Value, indentation + options.Indentation);
                        }
                    }
                    else
                    {
                        lines.AddRange(prop.Key.GetLines().Select(line => new KeyItemLine
                        {
                            Indentation = indentation,
                            Value = line
                        }));
                        ProcessNode(prop.Value!, indentation + options.Indentation);
                    }
                }
            }
            else
            {
                throw new NestedTextSerializeException($"Unexpected kind {node.GetValueKind()}.");
            }
        }
        ProcessNode(node, 0);
        return new CstOld(lines);
    }
    private static void AppendInlineValue(StringBuilder stringBuilder, JsonNode node)
    {
        switch (node.GetValueKind())
        {
            case JsonValueKind.String:
                stringBuilder.Append(node.GetValue<string>());
                break;
            case JsonValueKind.Array:
                stringBuilder.Append("[");
                foreach (var (child, i) in node.AsArray().Select((x, i) => (x, i)))
                {
                    if (i > 0) stringBuilder.Append(", ");
                    AppendInlineValue(stringBuilder, child);
                }
                stringBuilder.Append("]");
                break;
            case JsonValueKind.Object:
                stringBuilder.Append("{");
                foreach (var (prop, i) in node.AsObject().Select((x, i) => (x, i)))
                {
                    if (i > 0) stringBuilder.Append(", ");
                    stringBuilder.Append(prop.Key).Append(": ");
                    AppendInlineValue(stringBuilder, prop.Value);
                }
                stringBuilder.Append("}");
                break;
        }
    }
    private static string EmitInlineValue(JsonNode element)
    {
        var stringBuilder = new StringBuilder();
        AppendInlineValue(stringBuilder, element);
        return stringBuilder.ToString();
    }

    private static JsonNode ParseInlineValue(string content, int columnNumber, int lineNumber)
    {
        int pointer = 0;
        char Peek()
        {            
            while (pointer < content.Length && char.IsWhiteSpace(content[pointer])) pointer++;
            if (pointer >= content.Length)
            {
                throw new NestedTextDeserializeException($"Unexpected end of inline value.", lineNumber, columnNumber + pointer);
            }
            return content[pointer];
        }
        void ReadExpected(char c)
        {
            while (pointer < content.Length && char.IsWhiteSpace(content[pointer])) pointer++;
            if (pointer >= content.Length)
            {
                throw new NestedTextDeserializeException($"Unexpected end of inline value, expected '{c}'.", lineNumber, columnNumber + pointer);
            }
            if (content[pointer] != c)
            {
                throw new NestedTextDeserializeException($"Unexpected '{content[pointer]}', expected '{c}'.", lineNumber, columnNumber + pointer);
            }
            pointer++;
        }
        string ReadString(bool isInsideDictionary)
        {
            var start = pointer;
            while (pointer < content.Length && content[pointer].IsValidInlineChar(isInsideDictionary))
            {
                pointer++;
            }
            return content[start..pointer].Trim();
        }
        JsonNode ReadValue(bool isInsideDictionary)
        {
            var c = Peek();
            if (c == '{')
            {
                pointer++;
                if (pointer < content.Length && content[pointer] == '}') {
                    pointer++;
                    return new JsonObject();
                }
                var key = ReadString(true);
                ReadExpected(':');
                var value = ReadValue(true);
                var props = new List<KeyValuePair<string, JsonNode?>>() { new(key, value)};
                while (Peek() == ',')
                {
                    pointer++;
                    key = ReadString(true);
                    ReadExpected(':');
                    value = ReadValue(true);
                    props.Add(new(key, value));
                }
                ReadExpected('}');

                return new JsonObject(props);
            }
            if (c == '[')
            {
                pointer++;
                if (pointer < content.Length && content[pointer] == ']')
                {
                    pointer++;
                    return new JsonArray();
                }
                List<JsonNode> items = [ReadValue(isInsideDictionary)];
                while (Peek() == ',')
                {
                    pointer++;
                    items.Add(ReadValue(isInsideDictionary));
                }
                ReadExpected(']');

                return new JsonArray(items.ToArray());
            }
            /*if (!c.IsValidInlineChar(isInsideDictionary))
            {
                throw new NestedTextDeserializeException($"Expected string value, but got '{Peek()}'.", lineNumber, columnNumber + pointer);
            }*/
            return JsonValue.Create(ReadString(isInsideDictionary).Trim());
        }
        var result = ReadValue(false);
        while (pointer < content.Length && char.IsWhiteSpace(content[pointer])) pointer++;
        if (pointer != content.Length)
        {
            throw new NestedTextDeserializeException($"Unexpected characters following an inline value.", lineNumber, columnNumber + pointer);
        }
        return result;
    }

    public static CstOld Parse(string source, NestedTextSerializerOptions? options = null)
    {
        options ??= new();
        List<Line> lines = [];
        foreach (var line in source.GetLines())
        {
            lines.Add(ParseLine(line, options.ParseTaglessStringLines ? lines.LastOrDefault() : null));
        }
        return new CstOld(lines);
    }

    private static Line ParseLine(string line, Line? previous)
    {
        var indentation = 0;
        while (indentation < line.Length && line[indentation] == ' ')
        {
            indentation++;
        }
        if (indentation == line.Length)
        {
            return new BlankLine { Indentation = indentation };
        }
        var c = line[indentation];
        if (char.IsWhiteSpace(c))
        {
            return new ErrorLine { Indentation = indentation, Value = line[indentation..] };
        }
        if ((previous is ValueLine vl && previous is not KeyItemLine && vl.Value != "" && vl.Indentation < indentation)
            || (previous is TaglessStringItemLine tsil && tsil.Indentation == indentation))
        {
            return new TaglessStringItemLine { Indentation = indentation, Value = line[indentation..] };
        }
        if (c == '[' || c == '{') return new InlineValueLine { Indentation = indentation, Value = line[indentation..] };
        if (c == '#') return new CommentLine { Indentation = indentation, Value = line[(indentation + 1)..] };
        string? value = null;
        if (indentation + 1 == line.Length) value = "";
        else if (line[indentation + 1] == ' ') value = line[(indentation + 2)..];
        if (value != null)
        {
            if (c == '>') return new StringItemLine { Indentation = indentation, Value = value };
            if (c == '-') return new ListItemLine { Indentation = indentation, Value = value };
            if (c == ':') return new KeyItemLine { Indentation = indentation, Value = value };
        }
        var colonSpaceIndex = line.IndexOf(": ", indentation);
        if (colonSpaceIndex > -1)
        {
            var key = line[indentation..colonSpaceIndex];
            value = line[(colonSpaceIndex + 2)..];
            return new DictionaryItemLine { Indentation = indentation, Key = key.TrimEnd(), Value = value };
        }
        if (line.EndsWith(':'))
        {
            return new DictionaryItemLine { Indentation = indentation, Key = line[indentation..^1].TrimEnd(), Value = "" };
        }
        return new ErrorLine { Indentation = indentation, Value = line[indentation..] };
    }

    public JsonNode ToJsonNode()
    {
        var errorLine = Lines.OfType<ErrorLine>().FirstOrDefault();
        if (errorLine != null)
        {
            throw new NestedTextDeserializeException("Unexpected line.", errorLine.LineNumber, errorLine.Indentation + 1);
        }
        List<Line> lines = Lines.Where(x => x is not BlankLine && x is not CommentLine).ToList();
        var pointer = 0;

        IEnumerable<T> ReadLinesOfType<T>()
            where T : Line
        {
            var parentIndentation = pointer == 0 ? -1 : lines[pointer-1].Indentation;
            var indentation = -1;
            while (pointer < lines.Count && lines[pointer] is T line && (indentation == -1 || line.Indentation == indentation))
            {
                pointer++;
                indentation = line.Indentation;
                if (indentation <= parentIndentation) yield break;
                yield return line;
            }
        }
        JsonNode? ReadInlineValue()
        {
            var parentIndentation = pointer == 0 ? -1 : lines[pointer - 1].Indentation;
            if (pointer < lines.Count && lines[pointer] is InlineValueLine ivl && ivl.Indentation > parentIndentation)
            {
                pointer++;
                return ParseInlineValue(ivl.Value, ivl.Indentation + 1, ivl.LineNumber);
            }
            return null;
        }
        JsonNode? ReadListOrDictionaryValue(ValueLine line)
        {
            if (line.Value == "") return ReadValue() ?? JsonValue.Create("");
            List<ValueLine> resultLines = [line, .. ReadLinesOfType<TaglessStringItemLine>()];
            return JsonValue.Create(resultLines.JoinLinesValues());
        }
        JsonNode? ReadStringValue()
        {
            var lines = ReadLinesOfType<StringItemLine>().ToList();
            return lines.Any() ? JsonValue.Create(lines.JoinLinesValues()) : null;
        }
        JsonNode? ReadListValue()
        {
            var lines = ReadLinesOfType<ListItemLine>();
            var values = lines.Select(ReadListOrDictionaryValue).ToArray();
            return values.Any() ? new JsonArray(values) : null;
        }
        JsonNode? ReadDictionaryValue()
        {
            try
            {
                Dictionary<string, JsonNode> props = [];
                var dictLines = ReadLinesOfType<KeyItemOrDictionaryItemLine>();
                List<string> keyLines = [];
                foreach (var line in dictLines)
                {
                    if (line is DictionaryItemLine dil)
                    {
                        if (keyLines.Any())
                        {
                            props.Add(keyLines.JoinLines(), JsonValue.Create(""));
                            keyLines.Clear();
                        }
                        props.Add(dil.Key, ReadListOrDictionaryValue(dil) ?? JsonValue.Create(""));

                    }
                    if (line is KeyItemLine kil)
                    {
                        keyLines.Add(kil.Value);
                        var value = ReadValue();
                        if (value != null)
                        {
                            props.Add(keyLines.JoinLines(), value);
                            keyLines.Clear();
                        }
                    }
                }
                if (keyLines.Any())
                {
                    throw new NestedTextDeserializeException("Multiline key requires value", lines[pointer - 1].LineNumber, lines[pointer - 1].Indentation + 1);
                }
                return props.Any() ? new JsonObject(props) : null;
            }
            catch (ArgumentException ex)
            {
                throw new NestedTextDeserializeException("Duplicate key", lines[pointer - 1].LineNumber, lines[pointer-1].Indentation + 1);
            }
        }
        JsonNode? ReadValue()
        {
            if (pointer >= lines.Count)
            {
                return null;
            }
            var parentIndentation = pointer == 0 ? -1 : lines[pointer-1].Indentation;
            var firstLine = lines[pointer];
            var indentation = firstLine.Indentation;
            if (pointer == 0 && indentation > 0)
            {
                throw new NestedTextDeserializeException("Unexpected indentation.", lines[pointer].LineNumber, 1);
            }
            if (indentation <= parentIndentation) return null;
            JsonNode? result = ReadInlineValue() ?? ReadStringValue() ?? ReadListValue() ?? ReadDictionaryValue();
            if (result != null && pointer < lines.Count && lines[pointer].Indentation > parentIndentation)
            {
                throw new NestedTextDeserializeException("Unexpected indentation.", lines[pointer].LineNumber, lines[pointer].Indentation < indentation ? Math.Max(0,parentIndentation) + 1 : indentation + 1);
            }
            return result;
        }
        var result = ReadValue() ?? JsonValue.Create("");
        if (pointer != lines.Count) throw new NestedTextDeserializeException($"Unexpected lines.", lines[pointer].LineNumber, lines[pointer].Indentation + 1);
        return result;
    }
}

internal abstract class Line
{
    public int LineNumber { get; set; } = 1;
    public required int Indentation { get; set; }
    public override string ToString()
    {
        return new string(' ', Indentation) + Tag + GetStringFollowingTag();
    }
    protected abstract string Tag { get; }
    protected abstract string GetStringFollowingTag();
}

internal class BlankLine : Line
{
    protected override string Tag => "";
    protected override string GetStringFollowingTag() => "";
}
internal abstract class ValueLine : Line {
    public required string Value { get; set; }
    protected override string GetStringFollowingTag() => Value;
}
internal class ErrorLine : ValueLine
{
    protected override string Tag => "";
}
internal class CommentLine : ValueLine
{
    protected override string Tag => "#";
}
internal class StringItemLine : ValueLine
{
    protected override string Tag => ">";
    protected override string GetStringFollowingTag()
    {
        return (Value == "" ? "" : " ") + Value;
    }
}
internal class TaglessStringItemLine : ValueLine
{
    protected override string Tag => "";
}
internal class ListItemLine : ValueLine
{
    protected override string Tag => "-";
    protected override string GetStringFollowingTag()
    {
        return (Value == "" ? "" : " ") + Value;
    }
}
internal abstract class KeyItemOrDictionaryItemLine : ValueLine { }
internal class KeyItemLine : KeyItemOrDictionaryItemLine
{
    protected override string Tag => ":";
    protected override string GetStringFollowingTag()
    {
        return (Value == "" ? "" : " ") + Value;
    }
}
internal class DictionaryItemLine : KeyItemOrDictionaryItemLine
{
    public required string Key { get; set; }
    protected override string Tag => ":";
    public override string ToString()
    {
        return new string(' ', Indentation) + Key + Tag + (Value == "" ? "" : " ") + Value;
    }
}
/// <summary>
/// An line representing an inline value. In this version of the CST,
/// we don't actually parse it. We only parse it when deserializing
/// or formatting.
/// </summary>
internal class InlineValueLine : ValueLine
{
    protected override string Tag => ""; // Technically, the tag is "{" or "[", but we emit that as part of the actual value
}

