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
        var cst = Parser.Parse(source, options);
        return cst.Transform(options ?? new()).ToString();
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
        try
        {
            if (data is JsonNode node)
            {
                return Block.FromJsonNode(node, options).ToString();
            }
            return Block.FromJsonNode(JsonSerializer.Deserialize<JsonNode>(JsonSerializer.Serialize(data, jsonOptions), jsonOptions)!, options).ToString();
        }
        catch (JsonException ex)
        {
            if (ex.Message.Contains("A possible object cycle detected."))
            {
                throw new NestedTextSerializeException(ex.Message, ex);
            }
            throw new NestedTextSerializeException("Unknown json exception encountered while serializing.", ex);
        }
        catch (Exception ex)
        {
            throw new NestedTextSerializeException("Unknown exception encountered while serializing.", ex);
        }
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
        try
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(jsonNode, jsonOptions), jsonOptions)!;
        }
        catch (JsonException ex)
        {
            if (ex.InnerException?.Message.Contains("get the value of a token type") ?? false)
            {
                throw new NestedTextDeserializeException(ex.InnerException!.Message.Replace("get the value of a token type", "interpret") + $" To deserialize boolean/numeric types, define custom converters or set {nameof(NestedTextSerializerOptions.UseDefaultConventions)} = true.", ex);
            }
            throw new NestedTextDeserializeException("Unknown json exception encountered while deserializing.", ex);
        }
        catch (Exception ex)
        {
            throw new NestedTextSerializeException("Unknown exception encountered while deserializing.", ex);
        }
    }

    private static (NestedTextSerializerOptions, JsonSerializerOptions) EnhanceOptions(NestedTextSerializerOptions? options, JsonSerializerOptions? jsonOptions)
    {
        jsonOptions ??= new();
        options ??= new();
        if (options.UseDefaultConventions)
        {
            jsonOptions = new JsonSerializerOptions(jsonOptions);
            jsonOptions.Converters.Insert(0, new BoolConverter());
            jsonOptions.Converters.Insert(1, new JsonStringEnumConverter(new LowerCaseNamingPolicy()));
            jsonOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
            jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            jsonOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
            jsonOptions.PropertyNamingPolicy = new LowerCaseNamingPolicy();
        }
        return (options, jsonOptions);
    }
}
