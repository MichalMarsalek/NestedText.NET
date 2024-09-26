using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NestedText.Cst;

internal abstract class Node
{
    /// <summary>
    /// All errors within the tree.
    /// </summary>
    public abstract IEnumerable<ParsingError> Errors { get; }

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
    public override IEnumerable<ParsingError> Errors
    {
        get
        {
            if (Block.Indentation > 0)
            {
                yield return Block.Lines.OfType<ValueLine>().First().ToError("Unexpected indentation.", 1);
            }
            foreach(var error in Block.Errors)
            {
                yield return error;
            }
            if (Unterminated)
            {
                yield return new ParsingError(1, 1, "Unterminated document.");
            }
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
}