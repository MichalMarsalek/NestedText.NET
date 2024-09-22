﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText;

internal abstract class CstNode
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
    internal abstract CstNode Transform(NestedTextSerializerOptions options, CstNode? parent);

    /// <summary>
    /// Emits the node.
    /// </summary>
    internal protected abstract StringBuilder Append(StringBuilder builder);
}

internal abstract class LineNode : CstNode
{
    public int Indentation { get; set; }
    public int LineNumber { get; set; }
    public BlockNode Nested { get; set; } = new BlockNode([]);
    public override IEnumerable<ParsingError> Errors => Nested?.Errors ?? Enumerable.Empty<ParsingError>();
    protected StringBuilder AppendIndentation(StringBuilder builder)
        => builder.Append(new string(' ', Indentation));
    internal ParsingError ToError(string message, int? indentation = null)
        => new ParsingError(LineNumber, (indentation ?? Indentation) + 1, message);
    protected IEnumerable<ParsingError> AllNestedToErrors(int expectedIndentation)
        => Nested.Lines.Where(line => line is not BlankLineNode && line is not CommentLineNode).Select(x => x.ToError("Unexpected indentation.", expectedIndentation));
}

internal class IgnoredLineNode : LineNode
{
    internal required string Content { get; set; }
    public override IEnumerable<ParsingError> Errors => Enumerable.Empty<ParsingError>();

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
    {
        return this;
    }

    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).AppendLine(Content);
}

internal class BlankLineNode : IgnoredLineNode { }
internal class CommentLineNode : IgnoredLineNode {
    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).AppendLine("#" + Content);
}
internal class ErrorLineNode : IgnoredLineNode
{
    public required string Message { get; set; }
}

internal class BlockNode : CstNode
{
    public IReadOnlyCollection<LineNode> Lines { get; private set; } = [];
    public MultilineKind? Kind { get; private set; }
    public int? Indentation { get; private set; }
    public BlockNode(IEnumerable<LineNode>? lines = null)
    {
        Lines = lines?.ToList() ?? [];
        foreach (var line in Lines)
        {
            Kind = line switch
            {
                StringLineNode => MultilineKind.String,
                TaglessStringLineNode => MultilineKind.TaglessString,
                ListItemNode => MultilineKind.List,
                DictionaryItemNode => MultilineKind.Dictionary,
                KeyItemNode => MultilineKind.Dictionary,
                _ => null
            };
            if (Kind != null)
            {
                Indentation = line.Indentation;
                break;
            }
        }
    }

    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            var kind = Kind;
            foreach(var line in Lines)
            {
                if (line is ErrorLineNode errorLine) yield return errorLine.ToError(errorLine.Message);
                if (line is IgnoredLineNode) continue;

                if (line.Indentation != Indentation) yield return line.ToError("Unexpected indentation.", Indentation);
                if (kind == MultilineKind.String)
                {
                    if (line is not StringLineNode) yield return line.ToError("Unexpected node.");
                }
                else if (kind == MultilineKind.TaglessString)
                {
                    if (line is not TaglessStringLineNode) yield return line.ToError("Unexpected node.");
                }
                else if (kind == MultilineKind.List)
                {
                    if (line is not ListItemNode) yield return line.ToError("Unexpected node.");
                }
                else if (kind == MultilineKind.Dictionary)
                {
                    if (line is not DictionaryItemNode && line is not KeyItemNode) yield return line.ToError("Unexpected node.");
                }
                foreach (var lineError in line.Errors) yield return lineError;
            }
        }
    }

    protected internal override StringBuilder Append(StringBuilder builder)
    {
        foreach (var line in Lines)
        {
            line.Append(builder);
        }
        return builder;
    }

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Traverses the CST and converts to JsonNode, ignoring all error nodes.
    /// Caller is expected to check the <see cref="Errors"/> first.
    /// </summary>
    /// <returns></returns>
    public JsonNode? ToJsonNode()
    {
        var kind = Kind;
        if (kind == MultilineKind.String)
        {
            return JsonValue.Create(Lines.OfType<StringLineNode>().Select(x => x.Value).JoinLines());
        }
        if (kind == MultilineKind.TaglessString)
        {
            return JsonValue.Create(Lines.OfType<TaglessStringLineNode>().Select(x => x.Value).JoinLines());
        }
        if (kind == MultilineKind.List)
        {
            return new JsonArray(Lines.OfType<ListItemNode>().Select(x => x.ToJsonNode()).ToArray());
        }
        if (kind == MultilineKind.Dictionary)
        {
            Dictionary<string, JsonNode> props = [];
            var dictLines = Lines.OfType<DictionaryLineNode>();
            List<string> keyLines = [];
            foreach (var line in dictLines)
            {
                if (line is DictionaryItemNode din)
                {
                    if (keyLines.Any())
                    {
                        props.Add(keyLines.JoinLines(), JsonValue.Create(""));
                        keyLines.Clear();
                    }
                    props.Add(din.Key, din.ToJsonNode());

                }
                if (line is KeyItemNode kin)
                {
                    keyLines.Add(kin.Key);
                    var value = kin.Nested?.ToJsonNode();
                    if (value != null)
                    {
                        props.Add(keyLines.JoinLines(), value);
                        keyLines.Clear();
                    }
                }
            }
            if (keyLines.Any())
            {
                // TODO report this as error
            }
            return new JsonObject(props!);
        }
        return null;
    }

    /// <summary>
    /// Creates a CST from JsonNode.
    /// </summary>
    public static BlockNode FromJsonNode(JsonNode node, NestedTextSerializerOptions options)
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

        BlockNode FromJsonNodeImpl(JsonNode node, int indentation)
        {
            if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                return new BlockNode(value.GetValue<string>()!.GetLines().Select(x => new StringLineNode
                {
                    Indentation = indentation,
                    Value = x
                }));
            }
            if ((node is JsonArray || node is JsonObject) && IsValidInlineValue(node, options.MaxDepthToInline, false))
            {
                return new BlockNode([InlineFromJsonNode(node)]);
            }
            if (node is JsonArray array)
            {
                return new BlockNode(
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
                            Nested = FromJsonNodeImpl(x, indentation + options.Indentation)
                        };
                            
                    })
                );
            }
            if (node is JsonObject obj)
            {
                return new BlockNode(
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
                                    Nested = FromJsonNodeImpl(prop.Value, indentation + options.Indentation)
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
                            res.Last().Nested = FromJsonNodeImpl(prop.Value, indentation + options.Indentation);
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
    public override IEnumerable<ParsingError> Errors => AllNestedToErrors(Indentation);

    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).Append(Value == "" ? ">" : "> ").AppendLine(Value);

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
    {
        throw new NotImplementedException();
    }
}

internal class TaglessStringLineNode : LineNode
{
    public required string Value { get; set; }
    public override IEnumerable<ParsingError> Errors => AllNestedToErrors(Indentation);

    protected internal override StringBuilder Append(StringBuilder builder)
        => AppendIndentation(builder).AppendLine(Value);

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
    {
        throw new NotImplementedException();
    }
}

internal abstract class DictionaryLineNode : LineNode { }

internal class ListItemNode : DictionaryLineNode
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
    protected internal override StringBuilder Append(StringBuilder builder)
    {
        AppendIndentation(builder).AppendLine(RestOfLine == null ? "-" : "- " + RestOfLine);
        return Nested?.Append(builder) ?? builder;
    }

    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            if (RestOfLine != null && Nested.Kind != null && Nested.Kind != MultilineKind.TaglessString)
            {
                return AllNestedToErrors(Indentation);
            }
            return Nested.Errors;
        }
    }

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
    {
        throw new NotImplementedException();
    }
}

internal class DictionaryItemNode : DictionaryLineNode
{
    public required string Key { get; set; }
    public string? RestOfLine { get; set; }

    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            if (RestOfLine != null && Nested.Kind != null && Nested.Kind != MultilineKind.TaglessString)
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

    protected internal override StringBuilder Append(StringBuilder builder)
    {
        AppendIndentation(builder).Append(Key).AppendLine(RestOfLine == null ? ":" : ": " + RestOfLine);
        return Nested?.Append(builder) ?? builder;
    }
    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
    {
        throw new NotImplementedException();
    }
}

internal class KeyItemNode : DictionaryLineNode
{
    public required string Key { set; get; }
    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
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

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
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

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
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

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
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

    internal override CstNode Transform(NestedTextSerializerOptions options, CstNode? parent)
    {
        throw new NotImplementedException();
    }

    public override StringBuilder AppendValue(StringBuilder builder)
        => builder.Append(Content);
}

public record ParsingError(int LineNumber, int ColumnNumber, string Message);
internal enum MultilineKind { String, TaglessString, List, Dictionary, Inline }