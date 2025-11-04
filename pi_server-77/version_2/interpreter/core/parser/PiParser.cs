using System;
using System.Collections.Generic;
using System.Text;
using PiServer.Services;
using PiServer.version_2.interpreter.core.syntax;

namespace PiServer.version_2.interpreter.core.parser
{
    public class PiParser
    {
        private readonly Lexer _lexer;
        private Token _currentToken;

        public PiParser(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be empty");

            _lexer = new Lexer(input);
            _currentToken = _lexer.NextToken();
        }

        public Process Parse()
        {
            var process = ParseExpression();
            if (_currentToken.Type != TokenType.EndOfInput)
                throw new Exception($"Unexpected token at end: {_currentToken.Type} at position {_currentToken.Position}");
            return process;
        }

        private Process ParseExpression()
        {
            var processes = new List<Process> { ParseSingleProcess() };

            while (_currentToken.Type == TokenType.Parallel)
            {
                Eat(TokenType.Parallel);
                processes.Add(ParseSingleProcess());
            }

            return processes.Count == 1 ? processes[0] : new ParallelProcess(processes);
        }

       private Process ParseIf()
        {
            Eat(TokenType.If);
            var condition = ParseCondition();
            Eat(TokenType.Then);
            var thenBranch = ParseSingleProcess(); // ✅ рекурсивный разбор
            Eat(TokenType.Else);
            var elseBranch = ParseSingleProcess(); // ✅ рекурсивный разбор
            return new IfElseProcess(condition, thenBranch, elseBranch);
        }


        private Condition ParseCondition()
        {
            var left = ParseArithmeticExpression();

            var op = _currentToken.Type;
            if (op != TokenType.Equals && op != TokenType.NotEquals &&
                op != TokenType.GreaterThan && op != TokenType.LessThan)
                throw new Exception($"Unexpected operator in condition: {op}");
            Eat(op);

            var right = ParseArithmeticExpression();

            return new Condition(left, op, right);
        }

// === Арифметический парсер ===
private ArithmeticExpression ParseArithmeticExpression()
{
    var expr = ParseTerm();

    while (_currentToken.Type == TokenType.Plus || _currentToken.Type == TokenType.Minus)
    {
        string op = _currentToken.Value;
        Eat(_currentToken.Type);
        expr = new BinaryExpr(op, expr, ParseTerm());
    }

    return expr;
}

private ArithmeticExpression ParseTerm()
{
    var expr = ParseFactor();

    while (_currentToken.Type == TokenType.Multiply || _currentToken.Type == TokenType.Divide)
    {
        string op = _currentToken.Value;
        Eat(_currentToken.Type);
        expr = new BinaryExpr(op, expr, ParseFactor());
    }

    return expr;
}

private ArithmeticExpression ParseFactor()
{
    if (_currentToken.Type == TokenType.Number)
    {
        int val = int.Parse(_currentToken.Value);
        Eat(TokenType.Number);
        return new NumberExpr(val);
    }

    if (_currentToken.Type == TokenType.Identifier)
    {
        string name = _currentToken.Value;
        Eat(TokenType.Identifier);
        return new VariableExpr(name);
    }

    if (_currentToken.Type == TokenType.OpenParen)
    {
        Eat(TokenType.OpenParen);
        var expr = ParseArithmeticExpression();
        Eat(TokenType.CloseParen);
        return expr;
    }

    throw new Exception($"Unexpected token in arithmetic expression: {_currentToken.Type}");
}





        private Process ParseSingleProcess()
        {
            return _currentToken.Type switch
            {
                TokenType.NullProcess => ParseNull(),
                TokenType.OpenBrace => ParseBracedRestriction(),
                TokenType.OpenParen => ParseParenthesized(),
                TokenType.Identifier => ParseAction(),
                TokenType.Let => ParseLet(),
                TokenType.If => ParseIf(),
                _ => throw new Exception($"Unexpected token: {_currentToken.Type}")
            };
        }

        private Process ParseAction()
        {
            var channel = _currentToken.Value;
            Eat(TokenType.Identifier);

            if (_currentToken.Type == TokenType.OutputOp)
                return ParseOutput(channel);
            if (_currentToken.Type == TokenType.InputOp)
                return ParseInput(channel);

            return new NullProcess();
        }

private Process ParseOutput(string channel)
{
    Eat(TokenType.OutputOp);
    Eat(TokenType.OpenBracket);

    // Собираем всё содержимое между [ и ]
    string message = ReadMessageContent();

    Eat(TokenType.CloseBracket);
    Eat(TokenType.Dot);

    return new OutputProcess(channel, message, ParseSingleProcess());
}

private string ReadMessageContent()
{
    // Если сразу закрывающая скобка — пустое сообщение
    if (_currentToken.Type == TokenType.CloseBracket)
    {
        return "";
    }

    var sb = new StringBuilder();
    int depth = 1; // учитываем, что мы уже внутри []

    while (_currentToken.Type != TokenType.EndOfInput && depth > 0)
    {
        // Встречаем вложенные [ ] (маловероятно, но на будущее)
        if (_currentToken.Type == TokenType.OpenBracket)
        {
            depth++;
            sb.Append(GetTokenSymbol(_currentToken));
            _currentToken = _lexer.NextToken();
            continue;
        }

        if (_currentToken.Type == TokenType.CloseBracket)
        {
            depth--;
            if (depth == 0)
                break; // не добавляем закрывающую ]
            sb.Append(GetTokenSymbol(_currentToken));
            _currentToken = _lexer.NextToken();
            continue;
        }

        // Добавляем текстовое представление текущего токена
        sb.Append(GetTokenSymbol(_currentToken));
        // Для удобочитаемости вставляем пробел между "сложными" элементами,
        // но не между символами вроде '(' и следующими токенами.
        sb.Append(" ");

        // Переходим к следующему токену
        _currentToken = _lexer.NextToken();
    }

    return sb.ToString().Trim();
}

private string GetTokenSymbol(Token token)
{
    // если токен содержит явное значение (идентификатор/число/строка), используем его
    if (!string.IsNullOrEmpty(token.Value))
        return token.Value;

    // иначе картографируем по типу
    return token.Type switch
    {
        TokenType.OpenParen => "(",
        TokenType.CloseParen => ")",
        TokenType.OpenBracket => "[",
        TokenType.CloseBracket => "]",
        TokenType.OpenBrace => "{",
        TokenType.CloseBrace => "}",
        TokenType.Dot => ".",
        TokenType.Plus => "+",
        TokenType.Minus => "-",
        TokenType.Multiply => "*",
        TokenType.Divide => "/",
        TokenType.InputOp => "?",
        TokenType.OutputOp => "!",
        TokenType.Parallel => "|",
        TokenType.Star => "*",
        TokenType.Lambda => "\\", // или "λ" по вкусу
        TokenType.Fun => "fun",
        TokenType.Arrow => "->",
        TokenType.Equals => "==",
        TokenType.NotEquals => "!=",
        TokenType.GreaterThan => ">",
        TokenType.LessThan => "<",
        TokenType.Def => ":=",
        _ => "" // безопасный запас
    };
}




        private Process ParseInput(string channel)
        {
            Eat(TokenType.InputOp);
            Eat(TokenType.OpenParen);
            var variable = _currentToken.Value;
            Eat(TokenType.Identifier);
            Eat(TokenType.CloseParen);
            Eat(TokenType.Dot);
            return new InputProcess(channel, variable, ParseSingleProcess());
        }

        private Process ParseParenthesized()
        {
            Eat(TokenType.OpenParen);
            var process = ParseExpression();
            Eat(TokenType.CloseParen);
            return process;
        }

        private Process ParseBracedRestriction()
        {
            Eat(TokenType.OpenBrace);
            Eat(TokenType.Star);
            var name = _currentToken.Value;
            Eat(TokenType.Identifier);
            Eat(TokenType.CloseBrace);
            return new RestrictionProcess(name, ParseExpression());
        }

        private Process ParseNull()
        {
            Eat(TokenType.NullProcess);
            return new NullProcess();
        }

        private Process ParseLet()
        {
            Eat(TokenType.Let);
            string varName = _currentToken.Value;
            Eat(TokenType.Identifier);
            Eat(TokenType.Def);
            Eat(TokenType.OpenParen);
            var lambda = ParseLambdaTerm();
            Eat(TokenType.CloseParen);
            string argVar = _currentToken.Value;
            Eat(TokenType.Identifier);
            Eat(TokenType.Dot);
            return new LetProcess(varName, lambda, argVar, ParseSingleProcess());
        }

        private void Eat(TokenType type)
        {
            if (_currentToken.Type != type)
                throw new Exception($"Expected {type}, got {_currentToken.Type} at position {_currentToken.Position}");
            _currentToken = _lexer.NextToken();
        }

        private LambdaTerm ParseLambdaTerm()
        {
            if (_currentToken.Type == TokenType.Lambda)
            {
                Eat(TokenType.Lambda);
                string param = _currentToken.Value;
                Eat(TokenType.Identifier);
                Eat(TokenType.Dot);
                return new LambdaAbs(param, ParseLambdaTerm());
            }

            var term = ParseLambdaAtom();
            while (_currentToken.Type == TokenType.Identifier || _currentToken.Type == TokenType.OpenParen)
                term = new LambdaApp(term, ParseLambdaAtom());
            return term;
        }

        private LambdaTerm ParseLambdaAtom()
        {
            if (_currentToken.Type == TokenType.OpenParen)
            {
                Eat(TokenType.OpenParen);
                var term = ParseLambdaTerm();
                Eat(TokenType.CloseParen);
                return term;
            }

            string varName = _currentToken.Value;
            Eat(TokenType.Identifier);
            return new LambdaVar(varName);
        }
    }
}
