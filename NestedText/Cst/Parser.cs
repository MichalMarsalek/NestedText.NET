namespace NestedText.Cst;

internal static class Parser
{
    internal static Block Parse(string source, NestedTextSerializerOptions? options = null)
    {
        options ??= new();
        var multilinesStack = new Stack<List<Line>>();
        var indentStack = new Stack<int?>();
        multilinesStack.Push(new List<Line>());
        indentStack.Push(0);
        var lineNumber = 0;
        Line? lastLine = null;
        var toBePlacedIgnoredLines = new List<IgnoredLine>();
        foreach (var rawLine in source.GetLines())
        {
            lineNumber++;
            lastLine = ParseLine(rawLine, options.ParseTaglessStringLines ? lastLine : null, out var lineIndent);
            lastLine.LineNumber = lineNumber;
            lastLine.Indentation = lineIndent;

            if (lastLine is IgnoredLine ignoredLine)
            {
                toBePlacedIgnoredLines.Add(ignoredLine);
            }
            else
            {
                var currentMultiline = multilinesStack.Peek();
                var currentIndent = indentStack.Peek();

                if (currentIndent == 0 && currentMultiline.Count == 0)
                {
                    indentStack.Pop();
                    indentStack.Push(currentIndent = lineIndent);
                }

                while (lineIndent < currentIndent && indentStack.Any())
                {
                    var terminatedMultiline = multilinesStack.Pop();
                    currentMultiline = multilinesStack.Peek();
                    indentStack.Pop();
                    currentIndent = indentStack.Peek();
                    var currentMultilineLast = currentMultiline.Last();
                    currentMultilineLast.Nested = new Block(terminatedMultiline);
                }

                if (lineIndent > currentIndent && !currentMultiline.Last().Nested.Lines.Any())
                {
                    multilinesStack.Push(new List<Line>(toBePlacedIgnoredLines) { lastLine });
                    toBePlacedIgnoredLines.Clear();
                    indentStack.Push(lineIndent);
                }
                else
                {
                    currentMultiline.Add(lastLine);
                }
            }
        }
        multilinesStack.Peek().AddRange(toBePlacedIgnoredLines);

        while (true)
        {
            var terminatedMultiline = multilinesStack.Pop();
            if (!multilinesStack.Any()) return new Block(terminatedMultiline);
            multilinesStack.Peek().Last().Nested = new Block(terminatedMultiline);
        }
    }

    private static Line ParseLine(string line, Line? previous, out int indentation)
    {
        indentation = 0;
        while (indentation < line.Length && line[indentation] == ' ')
        {
            indentation++;
        }
        if (indentation == line.Length)
        {
            return new BlankLine { Content = "" };
        }
        var c = line[indentation];
        if (char.IsWhiteSpace(c))
        {
            return new ErrorLine { Content = line[indentation..], Message = "Only spaces are allowed as indentation." };
        }
        if (previous is ListItemLine lin && lin.RestOfLine != null && lin.Indentation < indentation
            || previous is DictionaryItemLine din && din.RestOfLine != null && din.Indentation < indentation)
        {
            return new TaglessStringLine { Value = line[indentation..] };
        }
        if (c == '[' || c == '{') return ParseInline(line, indentation, (previous?.LineNumber ?? 0) + 1);
        if (c == '#') return new CommentLine { Content = line[(indentation + 1)..] };
        string? value = null;
        if (indentation + 1 == line.Length) value = "";
        else if (line[indentation + 1] == ' ') value = line[(indentation + 2)..];
        if (value != null)
        {
            if (c == '>') return new StringLine { Value = value };
            if (c == '-') return new ListItemLine { RestOfLine = value == "" ? null : value };
            if (c == ':') return new KeyItemLine { Key = value };
        }
        var colonSpaceIndex = line.IndexOf(": ", indentation);
        if (colonSpaceIndex > -1)
        {
            var key = line[indentation..colonSpaceIndex];
            value = line[(colonSpaceIndex + 2)..];
            return new DictionaryItemLine { Key = key.TrimEnd(), RestOfLine = value == "" ? null : value };
        }
        if (line.EndsWith(':'))
        {
            return new DictionaryItemLine { Key = line[indentation..^1].TrimEnd(), RestOfLine = null };
        }

        return new ErrorLine { Message = "Unrecognised line.", Content = line[indentation..] };
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
