using System.Text.Json;
using System.Text.Json.Serialization;

namespace NestedText.Converters
{
    public class BoolConverter : JsonConverter<bool>
    {
        private static readonly JsonException BoolParsingException = new("The boolean property could not be read from boolean string value (e.g. 'true'/'false').");

        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() switch
            {
                var value when string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase) => true,
                var value when string.Equals(value, bool.FalseString, StringComparison.OrdinalIgnoreCase) => false,
                _ => throw BoolParsingException
            },
            _ => throw BoolParsingException
        };

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }
}
