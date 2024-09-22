namespace NestedText;

public class NestedTextDeserializeException : Exception
{
    public NestedTextDeserializeException()
        : base($"Parsing error, see {nameof(FirstError)} & {nameof(OtherErrors)} for detail.") { }
    public required ParsingError FirstError { get; set; }
    public required IEnumerable<ParsingError> OtherErrors { get; set; }
}
