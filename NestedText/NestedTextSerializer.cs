using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText;

public static class NestedTextSerializer
{
    /// <summary>
    /// Formats a NestedText document.
    /// For valid documents with no comments, Format(x) is equalent to Serialize(Deserialize(x)),
    /// but Format never errors and it preserves comments.
    /// </summary>
    /// <param name="source">A document to format.</param>
    /// <param name="options">NestedText options to use.</param>
    /// <returns>Formatted source.</returns>
    public static string Format(string source, NestedTextSerializerOptions? options = null)
    {
        return Parser.Parse(source, options).Transform(options ?? new()).ToString();
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
            return Cst.FromJsonNode(node).ToString();
        }
        return Cst.FromJsonNode(JsonSerializer.Deserialize<JsonNode>(JsonSerializer.Serialize(data, jsonOptions), jsonOptions)!)
            .Transform(options ?? new()).ToString();
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
        var jsonNode = Parser.Parse(data, options).ToJsonNode();
        if (jsonNode is T result)
        {
            return result;
        }
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(jsonNode, jsonOptions), jsonOptions)!;
    }
}
