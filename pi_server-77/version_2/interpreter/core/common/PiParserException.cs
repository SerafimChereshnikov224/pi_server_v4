namespace PiServer.version_2.interpreter.core.common
{
    public class PiParserException : Exception
    {
        public int Position { get; }
        public string Context { get; }

        public PiParserException(string message, int position, string context)
            : base($"{message} at position {position} (context: '{context}')")
        {
            Position = position;
            Context = context;
        }
    }
}
