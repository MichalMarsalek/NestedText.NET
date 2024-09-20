namespace NestedText;

internal static class Extensions
{
    internal static bool IsValidEndOfLineValue(this string value)
        => !value.Contains('\n') && !value.Contains('\n');

    internal static IEnumerable<string> GetLines(this string value)
        => value.Split(["\n", "\r\n", "\r"], StringSplitOptions.None);

    internal static bool IsValidTaglessStringValue(this IEnumerable<string> value)
        => value.First() != "" && value.Skip(1).All(x => x != "" && x[0] != ' ');

    internal static string JoinLines(this IEnumerable<string> value)
        => string.Join(Environment.NewLine, value);

    internal static string JoinLinesValues(this IEnumerable<ValueLine> value)
        => string.Join(Environment.NewLine, value.Select(x => x.Value));

    internal static bool IsValidInlineChar(this char value, bool isInsideDictionary)
        => !"\n\r[]{},".Contains(value) && (!isInsideDictionary || value != ':');

    internal static bool IsValidInlineString(this string value, bool isInsideDictionary)
        => !value.StartsWith(' ') && !value.EndsWith(' ') && value.All(x => x.IsValidInlineChar(isInsideDictionary));

    internal static bool IsValidKey(this string value)
        // TODO handle unicode spaces
        => value != "" && value[0] != '[' && value[0] != '{' && value[0] != ' ' && value.All(x => x != '\n' && x != '\r') && !value.Contains("- ") && !value.Contains("> ") && !value.Contains(": ");
}