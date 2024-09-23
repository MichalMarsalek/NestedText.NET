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
    /// Transforms the CST so that when emitted, it is formatted
    /// according to <see cref="options"/>.
    /// </summary>
    /// <param name="options">Formatting options.</param>
    /// <param name="parent">Parent node. This is relevant mainly for indentation.</param>
    /// <returns>The transformed CST</returns>
    internal abstract Node Transform(NestedTextSerializerOptions options, Node? parent);

    /// <summary>
    /// Emits the node.
    /// </summary>
    internal protected abstract StringBuilder Append(StringBuilder builder);
}