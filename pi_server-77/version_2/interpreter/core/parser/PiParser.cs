using PiServer.version_2.interpreter.core.parser.PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using System;
using System.Collections.Generic;

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
            var processes = new List<Process>();
            processes.Add(ParseSingleProcess());

            while (_currentToken.Type == TokenType.Parallel)
            {
                Eat(TokenType.Parallel);
                processes.Add(ParseSingleProcess());
            }

            return processes.Count == 1
                ? processes[0]
                : new ParallelProcess(processes);
        }

        private Process ParseSingleProcess()
        {
            switch (_currentToken.Type)
            {
                case TokenType.NullProcess:
                    return ParseNull();

                case TokenType.OpenBrace:
                    return ParseBracedRestriction(); // Только для ограничений

                case TokenType.OpenParen:
                    return ParseParenthesized();

                case TokenType.Identifier:
                    return ParseAction();

                default:
                    throw new Exception($"Unexpected token: {_currentToken.Type} at position {_currentToken.Position}");
            }
        }

        private Process ParseAction()
        {
            var channel = _currentToken.Value;
            Eat(TokenType.Identifier);

            // Если после идентификатора не идет действие, считаем это нулевым процессом
            if (_currentToken.Type != TokenType.OutputOp && _currentToken.Type != TokenType.InputOp)
                return new NullProcess();

            if (_currentToken.Type == TokenType.OutputOp)
                return ParseOutput(channel);

            return ParseInput(channel);
        }

        private Process ParseOutput(string channel)
        {
            Eat(TokenType.OutputOp);
            Eat(TokenType.OpenBracket);
            var message = _currentToken.Value;
            Eat(TokenType.Identifier);
            Eat(TokenType.CloseBracket);
            Eat(TokenType.Dot);
            return new OutputProcess(channel, message, ParseSingleProcess());
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

        private Process ParseRestriction()
        {
            Eat(TokenType.Star); // Съедаем '*'
            var name = _currentToken.Value;
            Eat(TokenType.Identifier); // Съедаем имя переменной
            return new RestrictionProcess(name, ParseExpression());
        }

        private Process ParseBracedRestriction()
        {
            Eat(TokenType.OpenBrace);
            Eat(TokenType.Star); // Обязательно должен быть *

            var name = _currentToken.Value;
            Eat(TokenType.Identifier);

            Eat(TokenType.CloseBrace); // Закрывающая скобка

            return new RestrictionProcess(name, ParseExpression());
        }

        private Process ParseNull()
        {
            Eat(TokenType.NullProcess);
            return new NullProcess();
        }

        private Process ParseBracedExpression()
        {
            Eat(TokenType.OpenBrace);

            // Если внутри фигурных скобок идет ограничение (*)
            if (_currentToken.Type == TokenType.Star)
            {
                var restriction = ParseRestriction();
                Eat(TokenType.CloseBrace);
                return restriction;
            }

            // Иначе обрабатываем как обычное выражение в скобках
            var process = ParseExpression();
            Eat(TokenType.CloseBrace);
            return process;
        }

        private void Eat(TokenType type)
        {
            if (_currentToken.Type == type)
            {
                _currentToken = _lexer.NextToken();
            }
            else
            {
                throw new Exception($"Expected {type}, got {_currentToken.Type} at position {_currentToken.Position}");
            }
        }
    }
}