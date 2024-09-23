using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText.Cst;

internal enum BlockKind { String, TaglessString, List, Dictionary, Inline }
internal class Block : Node
{
    public IReadOnlyCollection<Line> Lines { get; private set; } = [];
    public BlockKind? Kind { get; private set; }
    public int? Indentation { get; private set; }
    public Block(IEnumerable<Line>? lines = null)
    {
        Lines = lines?.ToList() ?? [];
        foreach (var line in Lines)
        {
            Kind = line switch
            {
                StringLine => BlockKind.String,
                TaglessStringLine => BlockKind.TaglessString,
                ListItemLine => BlockKind.List,
                DictionaryItemLine => BlockKind.Dictionary,
                KeyItemLine => BlockKind.Dictionary,
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
            foreach (var line in Lines)
            {
                if (line is ErrorLine errorLine) yield return errorLine.ToError(errorLine.Message);
                if (line is IgnoredLine) continue;

                if (line.Indentation != Indentation) yield return line.ToError("Unexpected indentation.", Indentation);
                if (Kind == BlockKind.String)
                {
                    if (line is not StringLine) yield return line.ToError("Unexpected node.");
                }
                else if (Kind == BlockKind.TaglessString)
                {
                    if (line is not TaglessStringLine) yield return line.ToError("Unexpected node.");
                }
                else if (Kind == BlockKind.List)
                {
                    if (line is not ListItemLine) yield return line.ToError("Unexpected node.");
                }
                else if (Kind == BlockKind.Dictionary)
                {
                    if (line is not DictionaryItemLine && line is not KeyItemLine) yield return line.ToError("Unexpected node.");
                }
                foreach (var lineError in line.Errors) yield return lineError;
            }
            if (Kind == BlockKind.Dictionary)
            {
                var dictLines = Lines.OfType<DictionaryLine>();
                List<Line> keyLines = [];
                foreach (var line in dictLines)
                {
                    if (line is DictionaryItemLine din)
                    {
                        if (keyLines.Any())
                        {
                            yield return keyLines.Last().ToError("Key item requires a value.");
                        }

                    }
                    if (line is KeyItemLine kin)
                    {
                        keyLines.Add(kin);
                        if (kin.NestedHasValue)
                        {
                            keyLines.Clear();
                        }
                    }
                }
                if (keyLines.Any())
                {
                    yield return keyLines.Last().ToError("Key item requires a value.");
                }
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

    internal override Node Transform(NestedTextSerializerOptions options, Node? parent)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Traverses the CST and converts to JsonNode, ignoring all error nodes.
    /// Caller is expected to check the <see cref="Errors"/> first.
    /// </summary>
    public JsonNode? ToJsonNode()
    {
        var kind = Kind;
        if (kind == BlockKind.String)
        {
            return JsonValue.Create(Lines.OfType<StringLine>().Select(x => x.Value).JoinLines());
        }
        if (kind == BlockKind.TaglessString)
        {
            return JsonValue.Create(Lines.OfType<TaglessStringLine>().Select(x => x.Value).JoinLines());
        }
        if (kind == BlockKind.List)
        {
            return new JsonArray(Lines.OfType<ListItemLine>().Select(x => x.ToJsonNode()).ToArray());
        }
        if (kind == BlockKind.Dictionary)
        {
            Dictionary<string, JsonNode> props = [];
            var dictLines = Lines.OfType<DictionaryLine>();
            List<string> keyLines = [];
            foreach (var line in dictLines)
            {
                if (line is DictionaryItemLine din)
                {
                    props.Add(din.Key, din.ToJsonNode());

                }
                if (line is KeyItemLine kin)
                {
                    keyLines.Add(kin.Key);
                    var value = kin.Nested.ToJsonNode();
                    if (value != null)
                    {
                        props.Add(keyLines.JoinLines(), value);
                        keyLines.Clear();
                    }
                }
            }
            return new JsonObject(props!);
        }
        return null;
    }

    /// <summary>
    /// Creates a CST from JsonNode.
    /// </summary>
    public static Block FromJsonNode(JsonNode node, NestedTextSerializerOptions options)
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
                return new InlineDictionary
                {
                    Indentation = 0,
                    KeyValues = obj.Select(x => new KeyValuePair<InlineString, Inline>(new InlineString
                    {
                        Indentation = 0,
                        Value = x.Key,
                        TrailingSpaces = 0
                    }, InlineFromJsonNode(x.Value))),
                    TrailingSpaces = 0
                };
            }
            throw new NestedTextSerializeException($"Unexpected kind {node.GetValueKind()}.");
        }

        Block FromJsonNodeImpl(JsonNode node, int indentation)
        {
            if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                return new Block(value.GetValue<string>()!.GetLines().Select(x => new StringLine
                {
                    Indentation = indentation,
                    Value = x
                }));
            }
            if ((node is JsonArray || node is JsonObject) && IsValidInlineValue(node, options.MaxDepthToInline, false))
            {
                return new Block([InlineFromJsonNode(node)]);
            }
            if (node is JsonArray array)
            {
                return new Block(
                    array.Select(x =>
                    {
                        if (x is JsonValue xValue && xValue.GetValueKind() == JsonValueKind.String)
                        {
                            var xString = xValue.GetValue<string>();
                            if (xString.IsValidEndOfLineValue()) return new ListItemLine
                            {
                                Indentation = indentation,
                                RestOfLine = xString.EmptyToNull()
                            };
                        }
                        return new ListItemLine
                        {
                            Indentation = indentation,
                            Nested = FromJsonNodeImpl(x, indentation + options.Indentation)
                        };

                    })
                );
            }
            if (node is JsonObject obj)
            {
                return new Block(
                    obj.SelectMany<KeyValuePair<string, JsonNode?>, Line>(prop =>
                    {
                        if (prop.Key.IsValidKey())
                        {
                            if (prop.Value!.GetValueKind() == JsonValueKind.String && prop.Value.GetValue<string>().IsValidEndOfLineValue())
                            {
                                return [new DictionaryItemLine
                                {
                                    Indentation = indentation,
                                    Key = prop.Key,
                                    RestOfLine = prop.Value.GetValue<string>().EmptyToNull()
                                }];
                            }
                            else
                            {
                                return [new DictionaryItemLine
                                {
                                    Indentation = indentation,
                                    Key = prop.Key,
                                    Nested = FromJsonNodeImpl(prop.Value, indentation + options.Indentation)
                                }];
                            }
                        }
                        else
                        {
                            var res = prop.Key.GetLines().Select(line => new KeyItemLine
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