using System;
using PiServer.version_2.interpreter.core.parser;

namespace PiServer.version_2.interpreter.core.syntax
{
    public class Condition
    {
        public ArithmeticExpression Left { get; }
        public TokenType Operator { get; }
        public ArithmeticExpression Right { get; }

        public Condition(ArithmeticExpression left, TokenType op, ArithmeticExpression right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }

        public bool Evaluate(PiEnvironment env)
        {
            object leftVal = Left.Evaluate(env);
            object rightVal = Right.Evaluate(env);

            // Приводим значения к числам, если это возможно
            double left = Convert.ToDouble(leftVal);
            double right = Convert.ToDouble(rightVal);

            return Operator switch
            {
                TokenType.Equals => Math.Abs(left - right) < double.Epsilon,
                TokenType.NotEquals => Math.Abs(left - right) >= double.Epsilon,
                TokenType.GreaterThan => left > right,
                TokenType.LessThan => left < right,
                _ => throw new Exception($"Unknown operator {Operator}")
            };
        }

        public override string ToString() => $"{Left} {Operator} {Right}";
    }
}
