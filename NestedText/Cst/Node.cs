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