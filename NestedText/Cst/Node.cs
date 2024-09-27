using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText.Cst;

internal record UnterminatedDocumentParsingError(int LineNumber, int ColumnNumber, string Message) : ParsingError(LineNumber, ColumnNumber, Message)
{
}

internal abstract class Node
{
    protected List<ParsingError>? errors;
    public abstract IEnumerable<ParsingError> CalcErrors();

    /// <summary>
    /// All errors within the tree.
    /// </summary>
    public IEnumerable<ParsingError> Errors
        => errors ??= CalcErrors().ToList();

    protected int? depth;
    public abstract int CalcDepth();

    /// <summary>
    /// Depth of the tree.
    /// </summary>
    public int Depth
        => depth ??= CalcDepth();

    protected List<CommentLine>? comments;
    public abstract List<CommentLine> CalcComments();

    /// <summary>
    /// Comment lines in the tree.
    /// </summary>
    public List<CommentLine> Comments
        => comments ??= CalcComments();


    public override string ToString()
    {
        var builder = new StringBuilder();
        Append(builder);
        return builder.ToString();
    }
    
    /// <summary>
    /// Emits the node.
    /// </summary>
    internal protected abstract StringBuilder Append(StringBuilder builder);
}

internal class Root : Node
{
    public required Block Block { get; init; }
    public required bool Unterminated { get; init; }
    public override IEnumerable<ParsingError> CalcErrors()
    {
        if (Block.Indentation > 0)
        {
            yield return Block.Lines.OfType<ValueLine>().First().ToError("Top-level content must start in column 1.", 0);
        }
        foreach(var error in Block.Errors)
        {
            yield return error;
        }
        if (Unterminated)
        {
            yield return new UnterminatedDocumentParsingError(1, 1, "Unterminated document.");
        }
    }

    protected internal override StringBuilder Append(StringBuilder builder)
    {
        Block.Append(builder);
        if (Unterminated) builder.Length -= Environment.NewLine.Length;
        return builder;
    }

    public Root Transform(NestedTextSerializerOptions options)
    {
        var fmt = options.FormatOptions;
        return fmt.SkipAll ? this : new Root { Block = Block.Transform(options, 0), Unterminated = fmt.SkipTermination ? Unterminated : false };
    }

    public JsonNode? ToJsonNode() => Block.ToJsonNode();
    public override int CalcDepth() => Block.Depth;
    public override List<CommentLine> CalcComments() => Block.Comments;
}