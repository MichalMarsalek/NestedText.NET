namespace NestedText;

internal static class Extensions
{
    internal static bool IsValidRestOfLineValue(this string value)
        => !value.Contains('\n') && !value.Contains('\n');

    internal static IEnumerable<string> GetLines(this string value)
        => value.Split(new string[] { "\n", "\r\n", "\r" }, StringSplitOptions.None);

    internal static bool IsValidTaglessStringValue(this IEnumerable<string> value)
        => value.First() != "" && value.Skip(1).All(x => x != "" && x[0] != ' ');

    internal static string JoinLines(this IEnumerable<string> value)
        => string.Join(Environment.NewLine, value);

    internal static bool IsValidInlineChar(this char value, bool isInsideDictionary)
        => !"\n\r[]{},".Contains(value) && (!isInsideDictionary || value != ':');

    internal static bool IsValidInlineChar(this char? value, bool isInsideDictionary)
        => value != null && value.Value.IsValidInlineChar(isInsideDictionary);
    internal static bool IsValueTerminator(this char? value, bool isInsideDictionary)
        => value == null || "]},".Contains(value.Value) || (isInsideDictionary && value == ':');

    internal static bool IsValidInlineString(this string value, bool isInsideDictionary)
        => !value.StartsWith(' ') && !value.EndsWith(' ') && value.All(x => x.IsValidInlineChar(isInsideDictionary));

    internal static bool IsValidKey(this string v)
        => v != "" && v[0] != '[' && v[0] != '{' && !char.IsWhiteSpace(v[0]) && v[0] != '#' && v.All(x => x != '\n' && x != '\r') && !v.Contains("- ") && !v.Contains("> ") && !v.Contains(": ");

    internal static IEnumerable<T> Iterate<T>(this IEnumerator<T> enumerator)
    {
        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }

    internal static bool IsWhiteSpace(this string value)
        => string.IsNullOrWhiteSpace(value);
    internal static bool IsWhiteSpace(this char value)
        => char.IsWhiteSpace(value);

    internal static string? EmptyToNull(this string? value)
        => value == "" ? null : value;

    internal static Tout Max<Tin, Tout>(this IEnumerable<Tin> source, Func<Tin, Tout> func, Tout whenEmpty)
        => source.Any() ? source.Max(func)! : whenEmpty;
}