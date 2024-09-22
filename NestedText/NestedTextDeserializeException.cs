namespace NestedText;

public class NestedTextDeserializeException : Exception
{
    public NestedTextDeserializeException()
        : base($"Parsing error, see {nameof(FirstError)} & {nameof(OtherErrors)} for detail.") { }
    public required ParsingError FirstError { get; set; }
    public required IEnumerable<ParsingError> OtherErrors { get; set; }
}

public class NestedTextDeserializeExceptionOld : Exception
{
    public NestedTextDeserializeExceptionOld(string message, int line, int column) : base(message)
    {
        Line = line;
        Column = column;
    }
    public int Line { get; set; }
    public int Column { get; set; }
}
