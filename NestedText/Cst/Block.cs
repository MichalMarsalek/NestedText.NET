using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace NestedText.Cst;

internal enum BlockKind { String, List, Dictionary, Inline }
internal class Block : Node
{
    public List<Line> Lines { get; private set; } = [];
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
                ListItemLine => BlockKind.List,
                DictionaryItemLine => BlockKind.Dictionary,
                KeyItemLine => BlockKind.Dictionary,
                InlineLine => BlockKind.Inline,
                _ => null
            };
            if (Kind != null)
            {
                Indentation = line.Indentation;
                break;
            }
        }
    }
    public override int CalcDepth() => 1 + Lines.Max(x => x.Depth, -14);
    public override List<CommentLine> CalcComments() => Lines.SelectMany(x => x.Comments).ToList();

    public override IEnumerable<ParsingError> CalcErrors()
    {
        bool inlineAlreadySeen = false;
        foreach (var line in Lines)
        {
            if (line is ErrorLine errorLine) yield return errorLine.ToError(errorLine.Message);
            if (line is IgnoredLine) continue;

            if (line.Indentation != Indentation) yield return line.ToError("Invalid indentation, partial dedent.", Indentation);
            if (Kind == BlockKind.String)
            {
                if (line is not StringLine) yield return line.ToError("Expected string item.");
            }
            else if (Kind == BlockKind.List)
            {
                if (line is not ListItemLine) yield return line.ToError("Expected list item.");
            }
            else if (Kind == BlockKind.Dictionary)
            {
                if (line is not DictionaryItemLine && line is not KeyItemLine) yield return line.ToError("Expected dictionary item.");
            }
            else if (Kind == BlockKind.Inline)
            {
                if (line is not InlineLine) yield return line.ToError("Unexpected node after inline value. Extra content.");
                if (inlineAlreadySeen)
                {
                    yield return line.ToError("Only one inline line allowed in a block.");
                }
                inlineAlreadySeen = true;
            }
            foreach (var lineError in line.Errors) yield return lineError;
        }
        if (Kind == BlockKind.Dictionary)
        {
            var keys = new HashSet<string>();
            var dictLines = Lines.OfType<DictionaryLine>();
            List<KeyItemLine> keyLines = [];
            foreach (var line in dictLines)
            {
                if (line is DictionaryItemLine din)
                {
                    if (keyLines.Any())
                    {
                        yield return keyLines.Last().ToError("Multiline key requires a value.");
                    }
                    if (!keys.Add(din.Key))
                    {
                        yield return line.ToError($"Duplicate key: '{din.Key}'.");
                    }

                }
                if (line is KeyItemLine kin)
                {
                    keyLines.Add(kin);
                    if (kin.Nested.Kind != null)
                    {
                        var key = keyLines.Select(x => x.Key).JoinLines();
                        if (!keys.Add(key))
                        {
                            yield return line.ToError($"Duplicate key: '{key}'.");
                        }
                        keyLines.Clear();
                    }
                }
            }
            if (keyLines.Any())
            {
                yield return keyLines.Last().ToError("Multiline key requires a value.");
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

    internal Block Transform(NestedTextSerializerOptions options, int indentation)
    {
        var fmt = options.FormatOptions;
        if (fmt.SkipAll || !Lines.Any()) return this;

        if (fmt.MultilineToInline && (Kind == BlockKind.List || Kind == BlockKind.Dictionary) && !Errors.Any() && (options.MaxDepthToInline == null || Depth <= options.MaxDepthToInline.Value))
        {
            var firstValueLine = Lines.OfType<ValueLine>().FirstOrDefault();
            if (firstValueLine != null)
            {
                var leadingComments = Lines.TakeWhile(line => line.LineNumber < firstValueLine.LineNumber);
                var anyTrailingComments = Lines.SkipWhile(line => line.LineNumber < firstValueLine.LineNumber).Any(x => x.Comments.Any());
                if (!anyTrailingComments)
                {
                    var jsonNode = ToJsonNode()!;
                    if (IsValidInlineValue(jsonNode, options.MaxDepthToInline, false))
                    {
                        var inlineLine = new InlineLine { Indentation = indentation, Inline = InlineFromJsonNode(jsonNode) };
                        if (options.MaxLineLengthToInline == null || inlineLine.ToStringLength() <= options.MaxLineLengthToInline.Value)
                        {
                            return new Block([.. leadingComments, inlineLine]);
                        }
                    }
                }
            }
        }

        var pendingMultilineKey = false;
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            if (line is InlineLine inlineLine && fmt.InlineToMultiline && !inlineLine.Inline.Errors.Any())
            {
                if (options.MaxDepthToInline != null && options.MaxDepthToInline.Value < line.Depth || options.MaxLineLengthToInline != null && options.MaxLineLengthToInline.Value < line.ToStringLength())
                {
                    if (inlineLine.Inline is InlineList inlineList)
                    {
                        var newLines = inlineList.Values.Select(x => {
                            var newListItem = new ListItemLine
                            {
                                Indentation = indentation,
                            };
                            if (x is InlineString inlineString)
                            {
                                var stringValue = inlineString.Value;
                                if (options.UseRestOfLineStrings && stringValue.IsValidRestOfLineValue())
                                {
                                    newListItem.RestOfLine = stringValue;
                                }
                                else
                                {
                                    newListItem.Nested = new Block(stringValue.GetLines().Select(x => new StringLine { Indentation = indentation + options.Indentation, Value = x }));
                                }
                            }
                            else
                            {
                                x.LeadingWhiteSpace = "";
                                newListItem.Nested = new Block([new InlineLine { Indentation = 0, Inline = x }]).Transform(options, indentation + options.Indentation);
                            }
                            return newListItem;
                        });
                        Lines[i] = newLines.First();
                        Lines.InsertRange(i + 1, newLines.Skip(1));
                        i += inlineList.Values.Count() - 1;
                        continue;
                    }
                    if (inlineLine.Inline is InlineDictionary inlineDict)
                    {
                        var newLines = inlineDict.KeyValues.Select(x => {
                            var key = (InlineString)x[0];
                            var value = x[1];
                            var newDictItem = new DictionaryItemLine
                            {
                                Key = key.Value,
                                KeyTrailingWhiteSpace = key.Suffix,
                                Indentation = indentation,
                            };
                            if (value is InlineString inlineString)
                            {
                                var stringValue = inlineString.Value;
                                if (options.UseRestOfLineStrings && stringValue.IsValidRestOfLineValue())
                                {
                                    newDictItem.RestOfLine = stringValue;
                                }
                                else
                                {
                                    newDictItem.Nested = new Block(stringValue.GetLines().Select(x => new StringLine { Indentation = indentation + options.Indentation, Value = x }));
                                }
                            }
                            else
                            {
                                value.LeadingWhiteSpace = "";
                                newDictItem.Nested = new Block([new InlineLine { Indentation = 0, Inline = value }]).Transform(options, indentation + options.Indentation);
                            }
                            return newDictItem;
                        });
                        Lines[i] = newLines.First();
                        Lines.InsertRange(i + 1, newLines.Skip(1));
                        i += inlineDict.KeyValues.Count() - 1;
                        continue;
                    }
                }
            }
            if (fmt.DictionaryKeys)
            {
                if (line is KeyItemLine keyItemLine)
                {
                    if (!pendingMultilineKey && line.Nested.Kind != null && keyItemLine.Key.IsValidKey())
                    {
                        Lines[i] = new DictionaryItemLine
                        {
                            Indentation = line.Indentation,
                            Key = keyItemLine.Key,
                            KeyTrailingWhiteSpace = "",
                            LineNumber = line.LineNumber,
                            Nested = line.Nested
                        };
                    }
                    pendingMultilineKey = line.Nested.Kind == null;
                }
                else if (line is not IgnoredLine)
                {
                    pendingMultilineKey = true;
                }
            }
            Lines[i] = Lines[i].Transform(options, indentation);
        }
        return this;
    }

    /// <summary>
    /// Traverses the CST and converts to JsonNode, ignoring all error nodes.
    /// Caller is expected to check the <see cref="CalcErrors"/> first.
    /// </summary>
    public JsonNode? ToJsonNode()
    {
        var kind = Kind;
        if (kind == BlockKind.String)
        {
            return JsonValue.Create(Lines.OfType<StringLine>().Select(x => x.Value).JoinLines());
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
        if (kind == BlockKind.Inline)
        {
            return Lines.OfType<InlineLine>().First().Inline.ToJsonNode();
        }
        return null;
    }

    private static bool IsValidInlineValue(JsonNode node, int? maxDepth, bool isInsideDictionary)
    {
        if (node is JsonArray _array && _array.Count == 0) return true;
        if (node is JsonObject _obj && _obj.Count == 0) return true;
        if (node is JsonValue value)
        {
            return value.GetValueKind() == JsonValueKind.String && node.GetValue<string>()!.IsValidInlineString(isInsideDictionary);
        }
        if (maxDepth == 0) return false;
        return node switch
        {
            JsonArray array => array.All(x => x != null && IsValidInlineValue(x, maxDepth - 1, isInsideDictionary)),
            JsonObject obj => obj.All(x => x.Key.IsValidInlineString(true) && IsValidInlineValue(x.Value, maxDepth - 1, true)),
            _ => false
        };
    }

    private static Inline InlineFromJsonNode(JsonNode node, int leadingSpaces = 0)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            return new InlineString { LeadingWhiteSpace = new string(' ', leadingSpaces), Value = value.GetValue<string>() };
        }
        if (node is JsonArray array)
        {
            return new InlineList { LeadingWhiteSpace = new string(' ', leadingSpaces), Values = array.Select((x, i) => InlineFromJsonNode(x!, i > 0 ? 1 : 0)) };
        }
        if (node is JsonObject obj)
        {
            return new InlineDictionary
            {
                LeadingWhiteSpace = new string(' ', leadingSpaces),
                KeyValues = obj.Select((x, i) => new List<Inline>{
                        new InlineString
                        {
                            LeadingWhiteSpace = new string(' ', i > 0 ? 1 : 0),
                            Value = x.Key,
                        },
                        InlineFromJsonNode(x.Value, 1)
                    })
            };
        }
        throw new NestedTextSerializeException($"Unexpected kind {node.GetValueKind()}.");
    }

    /// <summary>
    /// Creates a CST from JsonNode.
    /// </summary>
    public static Block FromJsonNode(JsonNode node, NestedTextSerializerOptions options)
    {
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
                return new Block([new InlineLine { Indentation = indentation, Inline = InlineFromJsonNode(node) }]);
            }
            if (node is JsonArray array)
            {
                return new Block(
                    array.Select(x =>
                    {
                        if (x is JsonValue xValue && xValue.GetValueKind() == JsonValueKind.String)
                        {
                            var xString = xValue.GetValue<string>();
                            if (xString.IsValidRestOfLineValue()) return new ListItemLine
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
                            if (prop.Value!.GetValueKind() == JsonValueKind.String && prop.Value.GetValue<string>().IsValidRestOfLineValue())
                            {
                                return [new DictionaryItemLine
                                {
                                    Indentation = indentation,
                                    Key = prop.Key,
                                    KeyTrailingWhiteSpace = "",
                                    RestOfLine = prop.Value.GetValue<string>().EmptyToNull()
                                }];
                            }
                            else
                            {
                                return [new DictionaryItemLine
                                {
                                    Indentation = indentation,
                                    Key = prop.Key,
                                    KeyTrailingWhiteSpace = "",
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
            throw new NestedTextSerializeException($"Unexpected type {node.GetValueKind()}. To serialize boolean/numeric types, define custom converters or set {nameof(NestedTextSerializerOptions.UseDefaultConventions)} = true.");
        }
        return FromJsonNodeImpl(node, 0);
    }

    internal string? GetRestOfLineValidStringValue()
    {
        if (Kind != BlockKind.String) return null;
        string? result = null;
        foreach (var line in Lines)
        {
            if (line is StringLine stringLine)
            {
                if (result != null) return null;
                if (!stringLine.Value.IsValidRestOfLineValue()) return null;
                result = stringLine.Value;
            }
            else if (line is not BlankLine) return null;
            if (line.Nested.Lines.Any()) return null;
        }
        return result;
    }
}