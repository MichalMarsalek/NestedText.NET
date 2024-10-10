namespace NestedText;

public class NestedTextSerializeException : Exception
{
    public NestedTextSerializeException(string message, Exception? inner = null) : base(message, inner) { }
}
