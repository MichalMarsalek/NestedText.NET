using System.Text.Json.Nodes;

namespace NestedText;

internal static class Parser
{
    internal static ValueNode Parse(string source, NestedTextSerializerOptions? options = null)
    {
        options ??= new();
        var multilinesStack = new Stack<List<LineNode>>();
        var indentStack = new Stack<int?>();
        multilinesStack.Push(new List<LineNode>());
        indentStack.Push(0);
        var lineNumber = 0;
        LineNode? lastLine = null;
        var toBePlacedIgnoredLines = new List<IgnoredLineNode>();
        foreach (var rawLine in source.GetLines())
        {
            lineNumber++;
            lastLine = ParseLine(rawLine, options.ParseTaglessStringLines ? lastLine : null, out var lineIndent);
            lastLine.LineNumber = lineNumber;
            lastLine.Indentation = lineIndent;

            if (lastLine is IgnoredLineNode ignoredLine)
            {
                toBePlacedIgnoredLines.Add(ignoredLine);
            }
            else
            {
                var currentMultiline = multilinesStack.Peek();
                var currentIndent = indentStack.Peek();
                while (lineIndent < currentIndent && indentStack.Any())
                {
                    var terminatedMultiline = multilinesStack.Pop();
                    currentMultiline = multilinesStack.Peek();
                    indentStack.Pop();
                    currentIndent = indentStack.Peek();
                    currentMultiline.Last().Nested = new ValueNode(terminatedMultiline);
                }


                if (lineIndent > currentIndent)
                {
                    if (currentIndent == 0 && currentMultiline.Count == 0)
                    {
                        indentStack.Pop();
                        indentStack.Push(currentIndent = lineIndent);
                    }

                    multilinesStack.Push(new List<LineNode>(toBePlacedIgnoredLines) { lastLine });
                    toBePlacedIgnoredLines.Clear();
                    indentStack.Push(currentIndent);
                }
                else
                {
                    currentMultiline.Add(lastLine);
                }
            }
        }

        while (true)
        {
            var terminatedMultiline = multilinesStack.Pop();
            if (!multilinesStack.Any()) return new ValueNode(terminatedMultiline);
            multilinesStack.Peek().Last().Nested = new ValueNode(terminatedMultiline);
        }
    }

    private static LineNode ParseLine(string line, LineNode? previous, out int indentation)
    {
        indentation = 0;
        while (indentation < line.Length && line[indentation] == ' ')
        {
            indentation++;
        }
        if (indentation == line.Length)
        {
            return new BlankLineNode { Content = "" };
        }      
        var c = line[indentation];
        if (char.IsWhiteSpace(c))
        {
            return new ErrorLineNode { Content = line[indentation..], Message = "Only spaces are allowed as indentation." };
        }
        if ((previous is ListItemNode lin && lin.RestOfLine != null && lin.Indentation < indentation)
            || (previous is DictionaryItemNode din && din.RestOfLine != null && din.Indentation < indentation))
        {
            return new TaglessStringLineNode { Value = line[indentation..] };
        }
        if (c == '[' || c == '{') return ParseInline(line, indentation, (previous?.LineNumber ?? 0) + 1);
        if (c == '#') return new CommentLineNode { Content = line[(indentation + 1)..] };
        string? value = null;
        if (indentation + 1 == line.Length) value = "";
        else if (line[indentation + 1] == ' ') value = line[(indentation + 2)..];
        if (value != null)
        {
            if (c == '>') return new StringLineNode { Value = value };
            if (c == '-') return new ListItemNode { RestOfLine = value == "" ? null : value };
            if (c == ':') return new KeyItemNode { Key = value };
        }
        var colonSpaceIndex = line.IndexOf(": ", indentation);
        if (colonSpaceIndex > -1)
        {
            var key = line[indentation..colonSpaceIndex];
            value = line[(colonSpaceIndex + 2)..];
            return new DictionaryItemNode { Key = key.TrimEnd(), RestOfLine = value == "" ? null : value };
        }
        if (line.EndsWith(':'))
        {
            return new DictionaryItemNode { Key = line[indentation..^1].TrimEnd(), RestOfLine = null };
        }

        return new ErrorLineNode { Message = "Unrecognised line.", Content = line[indentation..] };
    }

    private static Inline ParseInline(string source, int indentation, int lineNumber)
    {
        var pointer = indentation;
        var columnNumber = indentation + 1;
        char Peek()
        {
            while (pointer < source.Length && char.IsWhiteSpace(source[pointer])) pointer++;
            if (pointer >= source.Length)
            {
                throw new NotImplementedException($"Unexpected end of inline value.");
            }
            return source[pointer];
        }
        void ReadExpected(char c)
        {
            while (pointer < source.Length && char.IsWhiteSpace(source[pointer])) pointer++;
            if (pointer >= source.Length)
            {
                throw new NotImplementedException($"Unexpected end of inline value.");
            }
            if (source[pointer] != c)
            {
                throw new NotImplementedException($"Unexpected end of inline value.");
            }
            pointer++;
        }
        string ReadString(bool isInsideDictionary)
        {
            var start = pointer;
            while (pointer < source.Length && source[pointer].IsValidInlineChar(isInsideDictionary))
            {
                pointer++;
            }
            return source[start..pointer].Trim();
        }
        Inline ReadValue(bool isInsideDictionary)
        {
            var c = Peek();
            if (c == '{')
            {
                pointer++;
                if (pointer < source.Length && source[pointer] == '}')
                {
                    pointer++;
                    return new InlineDictionary { KeyValues = [] };
                }
                var key = ReadString(true);
                ReadExpected(':');
                var value = ReadValue(true);
                var props = new List<KeyValuePair<InlineString, Inline>>() { new(new InlineString { Value = key }, value) };
                while (Peek() == ',')
                {
                    pointer++;
                    key = ReadString(true);
                    ReadExpected(':');
                    value = ReadValue(true);
                    props.Add(new(new InlineString { Value = key }, value));
                }
                ReadExpected('}');

                return new InlineDictionary { KeyValues = props };
            }
            if (c == '[')
            {
                pointer++;
                if (pointer < source.Length && source[pointer] == ']')
                {
                    pointer++;
                    return new InlineList { Values = new List<Inline>() };
                }
                List<Inline> items = [ReadValue(isInsideDictionary)];
                while (Peek() == ',')
                {
                    pointer++;
                    items.Add(ReadValue(isInsideDictionary));
                }
                ReadExpected(']');

                return new InlineList { Values = items };
            }
            /*if (!c.IsValidInlineChar(isInsideDictionary))
            {
                throw new NestedTextDeserializeException($"Expected string value, but got '{Peek()}'.", lineNumber, columnNumber + pointer);
            }*/
            return new InlineString { Value = ReadString(isInsideDictionary).Trim() };
        }
        var result = ReadValue(false);
        while (pointer < source.Length && char.IsWhiteSpace(source[pointer])) pointer++;
        if (pointer != source.Length)
        {
            throw new NotImplementedException($"Unexpected end of inline value.");
        }
        return result;
    }
}
