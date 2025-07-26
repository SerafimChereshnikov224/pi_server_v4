using PiServer.version_2.interpreter.core.parser.PiServer.version_2.interpreter.core.parser;

public class Lexer
{
    private readonly string _input;
    private int _position;

    public Lexer(string input)
    {
        _input = input;
        _position = 0;
    }

    public Token NextToken()
    {
        SkipWhitespace();
        if (_position >= _input.Length)
            return new Token(TokenType.EndOfInput, position: _position);

        var startPos = _position;
        var current = _input[_position];

        switch (current)
        {
            case '0': _position++; return new Token(TokenType.NullProcess, position: startPos);
            case '(': _position++; return new Token(TokenType.OpenParen, position: startPos);
            case ')': _position++; return new Token(TokenType.CloseParen, position: startPos);
            case '{': _position++; return new Token(TokenType.OpenBrace, position: startPos);
            case '}': _position++; return new Token(TokenType.CloseBrace, position: startPos);
            case '[': _position++; return new Token(TokenType.OpenBracket, position: startPos);
            case ']': _position++; return new Token(TokenType.CloseBracket, position: startPos);
            case '|': _position++; return new Token(TokenType.Parallel, position: startPos);
            case '*': _position++; return new Token(TokenType.Star, position: startPos);
            case '.': _position++; return new Token(TokenType.Dot, position: startPos);
            case '?': _position++; return new Token(TokenType.InputOp, position: startPos);
            case '!': _position++; return new Token(TokenType.OutputOp, position: startPos);
        }

        if (char.IsLetter(current))
        {
            var start = _position;
            while (_position < _input.Length && (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
                _position++;
            return new Token(TokenType.Identifier, _input.Substring(start, _position - start), start);
        }

        throw new Exception($"Unexpected character: '{current}' at position {startPos}");
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            _position++;
    }
}