namespace NestedText;

internal static class StringExtensions
{
    internal static bool IsValidEndOfLineValue(this string value)
        => !value.Contains('\n') && !value.Contains('\n');

    internal static IEnumerable<string> GetLines(this string value)
        => value.Split(["\n", "\r\n"], StringSplitOptions.None);

    internal static bool IsValidTaglessStringValue(this IEnumerable<string> value)
        => value.First() != "" && value.Skip(1).All(x => x != "" && x[0] != ' ');

    internal static bool IsValidInlineString(this string value, bool isInsideDictionary)
        => !value.StartsWith(' ') && !value.EndsWith(' ') && "\n\r[]{},".All(x => !value.Contains(x)) && !isInsideDictionary || !value.Contains(':');

    internal static bool IsValidKey(this string value)
        => throw new NotImplementedException();
}