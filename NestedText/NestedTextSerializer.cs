using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
[assembly: InternalsVisibleTo("NestedText.Tests")]

namespace NestedText;
public static class NestedTextSerializer
{
    /// <summary>
    /// Formats a NestedText document.
    /// For valid documents with no comments, Format(x) is equivalent to Serialize(Deserialize(x)),
    /// but Format never errors and it preserves comments.
    /// </summary>
    /// <param name="source">A document to format.</param>
    /// <param name="options">NestedText options to use.</param>
    /// <returns>Formatted source.</returns>
    public static string Format(string source, NestedTextSerializerOptions? options = null)
    {
        return Parser.Parse(source, options).Transform(options ?? new(), null).ToString();
    }

    /// <summary>
    /// Serializes a value to a NestedText format.
    /// </summary>
    /// <param name="data">Value to serialize.</param>
    /// <param name="options">NestedText options to use.</param>
    /// <param name="jsonOptions">Json options to use. This allows you share the options and custom converters that you use for Json serialization.</param>
    /// <returns>Serialized data.</returns>
    public static string Serialize<T>(T data, NestedTextSerializerOptions? options = null, JsonSerializerOptions? jsonOptions = null)
    {
        if (data is JsonNode node)
        {
            return BlockNode.FromJsonNode(node, options ?? new()).ToString();
        }
        return BlockNode.FromJsonNode(JsonSerializer.Deserialize<JsonNode>(JsonSerializer.Serialize(data, jsonOptions), jsonOptions)!, options ?? new()).ToString();
    }

    /// <summary>
    /// Deserializes a value from a NestedText format.
    /// </summary>
    /// <param name="data">NestedText content.</param>
    /// <param name="options">NestedText options to use.</param>
    /// <param name="jsonOptions">Json options to use. This allows you share the options and custom converters that you use for Json serialization.</param>
    /// <returns>Deserialized data.</returns>
    public static T Deserialize<T>(string data, NestedTextSerializerOptions? options = null, JsonSerializerOptions? jsonOptions = null)
    {
        var cst = Parser.Parse(data, options);
        var errors = (cst.Indentation > 0 ? new ParsingError[] { new(1, 1, "Unexpected indentation.") }.Concat(cst.Errors) : cst.Errors).GetEnumerator();
        if (errors.MoveNext())
        {
            throw new NestedTextDeserializeException
            {
                FirstError = errors.Current,
                OtherErrors = errors.Iterate()
            };
        }

        var jsonNode = cst.ToJsonNode();
        if (jsonNode is T result)
        {
            return result;
        }
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(jsonNode, jsonOptions), jsonOptions)!;
    }
}
