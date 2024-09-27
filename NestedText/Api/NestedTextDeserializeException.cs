using NestedText.Cst;

namespace NestedText;


public record ParsingError(int LineNumber, int ColumnNumber, string Message);
public class NestedTextDeserializeException : Exception
{
    public NestedTextDeserializeException(IEnumerable<ParsingError> errors)
        : base($"Line {errors.First().LineNumber}, Col {errors.First().ColumnNumber}: {errors.First().Message}")
    {
        Errors = errors;
    }
    public IEnumerable<ParsingError> Errors { get; set; }
}
