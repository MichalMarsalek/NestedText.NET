using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NestedText;

internal abstract class Node
{
    /// <summary>
    /// All errors within the tree.
    /// </summary>
    public abstract IEnumerable<ParsingError> Errors { get; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        Append(builder);
        return builder.ToString();
    }

    /// <summary>
    /// Transforms the CST so that when emitted, it is formatted
    /// according to <see cref="options"/>.
    /// </summary>
    /// <param name="options">Formatting options.</param>
    /// <param name="parent">Parent node. This is relevant mainly for indentation.</param>
    /// <returns>The transformed CST</returns>
    internal abstract Node Transform(NestedTextSerializerOptions options, Node? parent);

    /// <summary>
    /// Emits the node.
    /// </summary>
    internal protected abstract StringBuilder Append(StringBuilder builder);
}

internal abstract class LineNode : Node
{
    public required int Indentation { get; set; }
    protected StringBuilder AppendIndentation(StringBuilder builder)
        => builder.Append(new string(' ', Indentation));
}

internal class IgnoredLineNode : LineNode
{
    internal required string Content { get; set; }
    public override IEnumerable<ParsingError> Errors => Enumerable.Empty<ParsingError>();

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        return this;
    }

    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).AppendLine(Content);
}

internal class BlankLineNode : IgnoredLineNode { }
internal class CommentLineNode : IgnoredLineNode { }
internal class ErrorLineNode : IgnoredLineNode
{
    public required ParsingError Error { get; set; }
    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            yield return Error;
        }
    }
}

internal class ValueNode : Node
{
    public IEnumerable<LineNode> Lines { get; set; }
    public ValueNode(IEnumerable<LineNode> lines)
    {
        Lines = lines;
    }

    public override IEnumerable<ParsingError> Errors => Lines.SelectMany(x => x.Errors);

    protected internal override StringBuilder Append(StringBuilder builder)
    {
        foreach (var line in Lines)
        {
            line.Append(builder);
        }
        return builder;
    }

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Traverses the CST and converts to JsonNode, ignoring all error nodes.
    /// Users are expected to check the <see cref="Errors"/> first.
    /// </summary>
    /// <returns></returns>
    public JsonNode ToJsonNode()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a CST from JsonNode.
    /// </summary>
    public static ValueNode FromJsonNode(JsonNode node, NestedTextSerializerOptions options)
    {
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

        Inline InlineFromJsonNode(JsonNode node)
        {
            if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                return new InlineString { Indentation = 0, Value = value.GetValue<string>(), TrailingSpaces = 0 };
            }
            if (node is JsonArray array)
            {
                return new InlineList { Indentation = 0, Values = array.Select(InlineFromJsonNode), TrailingSpaces = 0 };
            }
            if (node is JsonObject obj)
            {
                return new InlineDictionary {
                    Indentation = 0,
                    KeyValues = obj.Select(x => new KeyValuePair<InlineString, Inline>(new InlineString {
                        Indentation = 0,
                        Value = x.Key,
                        TrailingSpaces = 0
                    }, InlineFromJsonNode(x.Value))),
                    TrailingSpaces = 0
                };
            }
            throw new NestedTextSerializeException($"Unexpected kind {node.GetValueKind()}.");
        }

        ValueNode FromJsonNodeImpl(JsonNode node, int indentation)
        {
            if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                return new ValueNode(value.GetValue<string>()!.GetLines().Select(x => new StringLineNode
                {
                    Indentation = indentation,
                    Value = x
                }));
            }
            if ((node is JsonArray || node is JsonObject) && IsValidInlineValue(node, options.MaxDepthToInline, false))
            {
                return new ValueNode([InlineFromJsonNode(node)]);
            }
            if (node is JsonArray array)
            {
                return new ValueNode(
                    array.Select(x => {
                        if (x is JsonValue xValue && xValue.GetValueKind() == JsonValueKind.String)
                        {
                            var xString = xValue.GetValue<string>();
                            if (xString.IsValidEndOfLineValue()) return new ListItemNode {
                                Indentation = indentation,
                                RestOfLine = xString
                            };
                        }
                        return new ListItemNode
                        {
                            Indentation = indentation,
                            Value = FromJsonNodeImpl(x, indentation + options.Indentation)
                        };
                            
                    })
                );
            }
            if (node is JsonObject obj)
            {
                return new ValueNode(
                    obj.SelectMany<KeyValuePair<string, JsonNode?>, LineNode>(prop =>
                    {
                        if (prop.Key.IsValidKey())
                        {
                            if (prop.Value!.GetValueKind() == JsonValueKind.String && prop.Value.GetValue<string>().IsValidEndOfLineValue())
                            {
                                return [new DictionaryItemNode {
                                    Indentation = indentation,
                                    Key = prop.Key,
                                    RestOfLine = prop.Value.GetValue<string>()                                
                                }];
                            }
                            else
                            {
                                return [new DictionaryItemNode
                                {
                                    Indentation = indentation,
                                    Key = prop.Key,
                                    Value = FromJsonNodeImpl(prop.Value, indentation + options.Indentation)
                                }];
                            }
                        }
                        else
                        {
                            var res = prop.Key.GetLines().Select(line => new KeyItemNode
                            {
                                Indentation = indentation,
                                Key = line,
                            }).ToList();
                            res.Last().Value = FromJsonNodeImpl(prop.Value, indentation + options.Indentation);
                            return res;
                        }
                    })
                );
            }
            throw new NestedTextSerializeException($"Unexpected kind {node.GetValueKind()}.");
        }
        return FromJsonNodeImpl(node, 0);
    }
}

internal class StringLineNode : LineNode
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> Errors => Enumerable.Empty<ParsingError>();

    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).Append(Value == "" ? ">" : "> ").AppendLine(Value);

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal class TaglessStringLineNode : LineNode
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> Errors => Enumerable.Empty<ParsingError>();

    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).AppendLine(Value);

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal class ListItemNode : LineNode
{
    public string? RestOfLine { get; set; }
    public ValueNode? Value { get; set; }
    public override IEnumerable<ParsingError> Errors => Value?.Errors ?? Enumerable.Empty<ParsingError>();
    protected internal override StringBuilder Append(StringBuilder builder)
    {
        AppendIndentation(builder).Append(RestOfLine == null ? "-" : "- " + RestOfLine);
        return Value?.Append(builder) ?? builder;
    }

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal class DictionaryItemNode : LineNode
{
    public required string Key { get; set; }
    public string? RestOfLine { get; set; }
    public ValueNode? Value { get; set; }
    public override IEnumerable<ParsingError> Errors => Value?.Errors ?? Enumerable.Empty<ParsingError>();
    protected internal override StringBuilder Append(StringBuilder builder)
    {
        AppendIndentation(builder).Append(Key).Append(RestOfLine == null ? ":" : ": " + RestOfLine);
        return Value?.Append(builder) ?? builder;
    }
    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
}

internal class KeyItemNode : LineNode
{
    public required string Key { set; get; }
    public ValueNode? Value { get; set; }
    public override IEnumerable<ParsingError> Errors => Value?.Errors ?? Enumerable.Empty<ParsingError>();
    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }
    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).Append(":").AppendLine(Key);
}

internal abstract class Inline : LineNode
{
    public int Column { get; set; }
    public int TrailingSpaces { get; set; } = 0;
    public abstract StringBuilder AppendValue(StringBuilder builder);
    public StringBuilder AppendNested(StringBuilder builder)
        => AppendValue(AppendIndentation(builder)).Append(new string(' ', TrailingSpaces));
    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendNested(AppendIndentation(builder)).AppendLine();
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

internal record RawLine(int LineNumber, int Indentation, string Value);
public record ParsingError(int LineNumber, int ColumnNumber, string Message);