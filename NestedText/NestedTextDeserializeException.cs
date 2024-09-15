namespace NestedText
{
    public class NestedTextDeserializeException : Exception
    {
        public NestedTextDeserializeException(string message, int line, int column) : base(message) { 
            Line = line;
            Column = column;
        }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
