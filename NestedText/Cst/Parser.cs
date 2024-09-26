namespace NestedText.Cst;

internal static class Parser
{
    internal static Block Parse(string source, NestedTextSerializerOptions? options = null)
    {
        var multilinesStack = new Stack<List<Line>>();
        var indentStack = new Stack<int?>();
        multilinesStack.Push(new List<Line>());
        indentStack.Push(0);
        var lineNumber = 0;
        var toBePlacedIgnoredLines = new List<IgnoredLine>();
        foreach (var rawLine in source.GetLines())
        {
            lineNumber++;
            var line = ParseLine(rawLine, lineNumber, out var lineIndent);
            line.LineNumber = lineNumber;
            line.Indentation = lineIndent;

            if (line is IgnoredLine ignoredLine)
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
                    var currentMultilineLast = currentMultiline.Last();
                    currentMultilineLast.Nested = new Block(terminatedMultiline);
                }

                if (lineIndent > currentIndent && currentMultiline.Any() && !currentMultiline.Last().Nested.Lines.Any())
                {
                    multilinesStack.Push(new List<Line>(toBePlacedIgnoredLines) { line });
                    toBePlacedIgnoredLines.Clear();
                    indentStack.Push(lineIndent);
                }
                else
                {
                    currentMultiline.Add(line);
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

    private static Line ParseLine(string line, int lineNumber, out int indentation)
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
        if (c == '[' || c == '{')
        {
            var inlineLine = new InlineLine() { Inline = default! };
            inlineLine.Inline = ParseInline(line, indentation, inlineLine);
            return inlineLine;
        } 
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

    private static Inline ParseInline(string source, int indentation, InlineLine line)
    {
        var pointer = indentation;
        var columnNumber = indentation + 1;
        char? Peek()
        {
            if (pointer >= source.Length)
            {
                return null;
            }
            return source[pointer];
        }
        string ReadString(bool isInsideDictionary)
        {
            var start = pointer;
            var lastNonWhiteSpace = pointer - 1;
            while (Peek().IsValidInlineChar(isInsideDictionary))
            {
                if (!Peek()!.Value.IsWhiteSpace())
                {
                    lastNonWhiteSpace = pointer;
                }
                pointer++;
            }
            pointer = lastNonWhiteSpace + 1;
            return source[start..pointer];
        }
        List<Inline> ReadDictionaryItem()
        {
            List<Inline> result = [ReadValue(true)];
            while (Peek() == ':')
            {
                pointer++;
                result.Add(ReadValue(true));
            }
            return result;
        }
        Inline ReadValue(bool isInsideDictionary, bool isRoot = false)
        {
            int leadingSpaces = 0;
            char? c;
            while ((c = Peek()) != null && char.IsWhiteSpace(c.Value))
            {
                pointer++;
                leadingSpaces++;
            }
            Inline result;
            var valueStart = pointer;
            if (c == null)
            {
                result = new InlineString { Line = line, LeadingSpaces = leadingSpaces, Value = "" };
            }
            else
            {

                if (c == '{')
                {
                    pointer++;
                    if (Peek() == '}')
                    {
                        pointer++;
                        result = new InlineDictionary { Line = line, KeyValues = [] };
                    }
                    else
                    {
                        List<List<Inline>> items = [ReadDictionaryItem()];
                        while (Peek() == ',')
                        {
                            pointer++;
                            items.Add(ReadDictionaryItem());
                        }
                        if (Peek() == '}')
                        {
                            pointer++;
                            result = new InlineDictionary { Line = line, KeyValues = items };
                        }
                        else
                        {
                            result = new InlineDictionary { Line = line, KeyValues = items, Unterminated = true };
                        }
                    }
                }
                else if (c == '[')
                {
                    pointer++;
                    if (Peek() == ']')
                    {
                        pointer++;
                        result = new InlineList { Line = line, Values = [] };
                    }
                    else
                    {
                        List<Inline> items = [ReadValue(false)];
                        while (Peek() == ',')
                        {
                            pointer++;
                            items.Add(ReadValue(false));
                        }
                        if (Peek() == ']')
                        {
                            pointer++;
                            result = new InlineList { Line = line, Values = items };
                        }
                        else
                        {
                            result = new InlineList { Line = line, Values = items, Unterminated = true };
                        }
                    }
                }
                else
                {
                    result = new InlineString { Line = line, Value = ReadString(isInsideDictionary) };
                }
            }
            result.ValueStart = valueStart;
            result.ValueEnd = pointer;
            if (isRoot) pointer = source.Length;
            else while (!Peek().IsValueTerminator(isInsideDictionary))
            {
                pointer++;
            }
            result.LeadingSpaces = leadingSpaces;
            result.Suffix = source[result.ValueEnd..pointer];
            return result;
        }
        var result = ReadValue(false, true);
        return result;
    }
}
