namespace PiServer.version_2.interpreter.core.parser
{
    namespace PiServer.version_2.interpreter.core.parser
    {
        public enum TokenType
        {
            NullProcess,
            OpenParen,      // (
            CloseParen,     // )
            OpenBrace,      // {
            CloseBrace,     // }
            OpenBracket,    // [
            CloseBracket,   // ]
            Parallel,       // |
            Star,           // * 
            Dot,            // .
            InputOp,        // ?
            OutputOp,       // !
            Identifier,
            EndOfInput,

            Number,
            Plus,
            Minus,
            Multiply,
            Divide,

            Lambda,
            
            Arrow, //->

            Fun,

            Def, //:=

            Let
        }

        public class Token
        {
            public TokenType Type { get; }
            public string Value { get; }
            public int Position { get; }

            public Token(TokenType type, string value = "", int position = -1)
            {
                Type = type;
                Value = value;
                Position = position;
            }
        }
    }
}
