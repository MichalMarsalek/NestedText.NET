using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NestedText.Converters
{
    public partial class LowerCaseNamingPolicy : JsonNamingPolicy
    {
        [GeneratedRegex("(?<!^)([A-Z][a-z]|(?<=[a-z])[^a-z]|(?<=[A-Z])[0-9_])")]
        private static partial Regex CamelCaseWordsRegex();

        public override string ConvertName(string name)
            => CamelCaseWordsRegex().Replace(name, " $1").ToLower();
    }
}
