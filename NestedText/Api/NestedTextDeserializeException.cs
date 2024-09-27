using NestedText.Cst;

namespace NestedText;


public record ParsingError(int LineNumber, int ColumnNumber, string Message);
public class NestedTextDeserializeException : Exception
{
    public NestedTextDeserializeException(ParsingError firstError, IEnumerable<ParsingError> otherErrors)
        : base($"Line {firstError.LineNumber}, Col {firstError.ColumnNumber}: {firstError.Message}")
    {
        FirstError = firstError;
        OtherErrors = otherErrors;
    }
    public ParsingError FirstError { get; set; }
    public IEnumerable<ParsingError> OtherErrors { get; set; }
}
