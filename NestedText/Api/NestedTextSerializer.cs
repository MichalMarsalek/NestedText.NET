using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NestedText.Converters;
using NestedText.Cst;
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
        (options, jsonOptions) = EnhanceOptions(options, jsonOptions);
        if (data is JsonNode node)
        {
            return Block.FromJsonNode(node, options).ToString();
        }
        return Block.FromJsonNode(JsonSerializer.Deserialize<JsonNode>(JsonSerializer.Serialize(data, jsonOptions), jsonOptions)!, options).ToString();
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
        (options, jsonOptions) = EnhanceOptions(options, jsonOptions);
        var cst = Parser.Parse(data, options);
        var errors = options.ThrowOnUnterminated ? cst.Errors : cst.Errors.Where(x => x is not UnterminatedDocumentParsingError);
        if (errors.Any())
        {
            throw new NestedTextDeserializeException(errors);
        }

        var jsonNode = cst.ToJsonNode();
        if (jsonNode is T result)
        {
            return result ?? (T)(options.EmptyType switch
            {
                EmptyType.String => (object)JsonValue.Create(""),
                EmptyType.List => (object)new JsonArray(),
                EmptyType.Dictionary => (object)new JsonObject()
            }); ;
        }
        if (jsonNode == null)
        {
            foreach (var empty in new string[] { "\"\"", "[]", "{}", "null" }) {
                try
                {
                    return JsonSerializer.Deserialize<T>(empty, jsonOptions)!;
                }
                catch { }
            }
        }
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(jsonNode, jsonOptions), jsonOptions)!;
    }

    private static (NestedTextSerializerOptions, JsonSerializerOptions) EnhanceOptions(NestedTextSerializerOptions? options, JsonSerializerOptions? jsonOptions)
    {
        jsonOptions ??= new();
        options ??= new();
        if (options.UseDefaultConventions)
        {
            jsonOptions = new JsonSerializerOptions(jsonOptions);
            jsonOptions.Converters.Insert(0, new BoolConverter());
            jsonOptions.Converters.Insert(1, new JsonStringEnumConverter(new SpaceCaseNamingPolicy()));
            jsonOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
            jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            jsonOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
            jsonOptions.PropertyNamingPolicy = new SpaceCaseNamingPolicy();
        }
        return (options, jsonOptions);
    }
}
