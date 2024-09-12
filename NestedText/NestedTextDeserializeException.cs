namespace NestedText
{
    public class NestedTextDeserializeException : Exception
    {
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
