using NestedText.Cst;

namespace NestedText;


public record ParsingError(int LineNumber, int ColumnNumber, string Message);
public class NestedTextDeserializeException : Exception
{
    public NestedTextDeserializeException()
        : base($"Parsing error, see {nameof(FirstError)} & {nameof(OtherErrors)} for detail.") { }
    public required ParsingError FirstError { get; set; }
    public required IEnumerable<ParsingError> OtherErrors { get; set; }
}
