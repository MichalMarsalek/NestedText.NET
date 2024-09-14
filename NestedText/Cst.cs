using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace NestedText;

internal class Cst(IEnumerable<Line> lines)
{
    public List<Line> Lines = lines.ToList();
    public Cst Transform(NestedTextSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return string.Join(Environment.NewLine, Lines);
    }

    public static Cst FromJsonNode(JsonNode node)
    {
        throw new NotImplementedException();
    }

    public JsonNode ToJsonNode()
    {
        throw new NotImplementedException();
    }
}

internal abstract class Line
{
    public int Indentation { get; set; }
    public override string ToString()
    {
        return new string(' ', Indentation) + Tag + GetStringFollowingTag();
    }
    protected abstract string Tag { get; }
    protected abstract string GetStringFollowingTag();
}

internal class BlankLine : Line
{
    protected override string Tag => "";
    protected override string GetStringFollowingTag() => "";
}
internal abstract class ValueLine : Line {
    public required string Value { get; set; }
    protected override string GetStringFollowingTag() => Value;
}
internal class CommentLine : ValueLine
{    
    protected override string Tag => "#";
}
internal class StringItemLine : ValueLine
{
    protected override string Tag => "> ";
}
internal class TaglessStringItemLine : ValueLine
{
    protected override string Tag => "";
}
internal class KeyItemLine : ValueLine
{
    protected override string Tag => ": ";
}
internal class DictionaryItemLine : ValueLine
{
    public required string Key { get; set; }
    protected override string Tag => ": ";
    public override string ToString()
    {
        return new string(' ', Indentation) + Key + Tag + Value;
    }
}
internal class InlineValueLine : ValueLine
{
    protected override string Tag => ""; // Technically, the tag is "{" or "[", but we emit that as part of the actual value
}

