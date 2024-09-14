using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NestedText;

internal class Cst(IEnumerable<Line> lines)
{
    public List<Line> Lines = lines.ToList();
    public Cst Transform(NestedTextSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return string.Join(Environment.NewLine, Lines);
    }

    public static Cst FromJsonElement(JsonElement element, NestedTextSerializerOptions options)
    {
        List<Line> lines = new();
        bool IsValidInlineValue(JsonElement element, int? maxDepth, bool isInsideDictionary)
        {
            if (maxDepth == 0) return false;
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!.IsValidInlineString(isInsideDictionary),
                JsonValueKind.Array => element.EnumerateArray().All(x => IsValidInlineValue(x, maxDepth - 1, isInsideDictionary)),
                JsonValueKind.Object => element.EnumerateArray().All(x => IsValidInlineValue(x, maxDepth - 1, true)),
                _ => false
            };
        }

        void ProcessElement(JsonElement element, int indentation)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    lines.AddRange(element.GetString()!.GetLines().Select(x => new StringItemLine
                    {
                        Indentation = indentation,
                        Value = x
                    }));
                    break;
                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    if (IsValidInlineValue(element, options.MaxDepthToInline, false))
                    {
                        var inlined = EmitInlineValue(element);
                        if (options.MaxLineLengthToInline == null || indentation + inlined.Length <= options.MaxLineLengthToInline)
                        {
                            lines.Add(new InlineValueLine
                            {
                                Indentation = indentation,
                                Value = inlined
                            });
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in element.EnumerateArray())
                        {
                            if (child.ValueKind == JsonValueKind.String && child.GetString()!.IsValidEndOfLineValue())
                            {
                                lines.Add(new ListItemLine
                                {
                                    Indentation = indentation,
                                    Value = child.GetString()!
                                });
                            }
                            else
                            {
                                lines.Add(new ListItemLine
                                {
                                    Indentation = indentation,
                                    Value = ""
                                });
                                ProcessElement(child, indentation + options.Indentation);
                            }
                        }
                    }
                    else
                    {
                        foreach (var prop in element.EnumerateObject())
                        {
                            if (prop.Name.IsValidKey())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String && prop.Value.GetString()!.IsValidEndOfLineValue())
                                {
                                    lines.Add(new DictionaryItemLine
                                    {
                                        Indentation = indentation,
                                        Key = prop.Name,
                                        Value = prop.Value.GetString()!
                                    });
                                }
                                else
                                {
                                    lines.Add(new DictionaryItemLine
                                    {
                                        Indentation = indentation,
                                        Key = prop.Name,
                                        Value = ""
                                    });
                                    ProcessElement(prop.Value, indentation + options.Indentation);
                                }
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        }
                    }
                    break;
                default: throw new Exception($"Unexpected kind {element.ValueKind}.");
            }
        }
        ProcessElement(element, 0);
        return new Cst(lines);
    }
    private static void AppendInlineValue(StringBuilder stringBuilder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                stringBuilder.Append(element.GetString());
                break;
            case JsonValueKind.Array:
                stringBuilder.Append("[");
                foreach (var (child, i) in element.EnumerateArray().Select((x, i) => (x, i)))
                {
                    if (i > 0) stringBuilder.Append(", ");
                    AppendInlineValue(stringBuilder, child);
                }
                stringBuilder.Append("}");
                break;
            case JsonValueKind.Object:
                stringBuilder.Append("{");
                foreach (var (prop, i) in element.EnumerateObject().Select((x, i) => (x, i)))
                {
                    if (i > 0) stringBuilder.Append(", ");
                    stringBuilder.Append(prop.Name).Append(": ");
                    AppendInlineValue(stringBuilder, prop.Value);
                }
                stringBuilder.Append("}");
                break;
        }
    }
    private static string EmitInlineValue(JsonElement element)
    {
        var stringBuilder = new StringBuilder();
        AppendInlineValue(stringBuilder, element);
        return stringBuilder.ToString();
    }

    private static JsonElement ParseInlineValue(string content)
    {
        throw new NotImplementedException();
    }

    public JsonElement ToJsonElement()
    {
        throw new NotImplementedException();
    }
}

internal abstract class Line
{
    public int Indentation { get; set; }
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
internal class CommentLine : ValueLine
{    
    protected override string Tag => "#";
}
internal class StringItemLine : ValueLine
{
    protected override string Tag => "> ";
}
internal class TaglessStringItemLine : ValueLine
{
    protected override string Tag => "";
}
internal class KeyItemLine : ValueLine
{
    protected override string Tag => ":";
}
internal class ListItemLine : ValueLine
{
    protected override string Tag => "-";
    protected override string GetStringFollowingTag()
    {
        return (Value == "" ? "" : " ") + Value;
    }
}
internal class DictionaryItemLine : ValueLine
{
    public required string Key { get; set; }
    protected override string Tag => ": ";
    public override string ToString()
    {
        return new string(' ', Indentation) + Key + Tag + (Value == "" ? "" : " ") + Value;
    }
}
internal class InlineValueLine : ValueLine
{
    protected override string Tag => ""; // Technically, the tag is "{" or "[", but we emit that as part of the actual value
}

