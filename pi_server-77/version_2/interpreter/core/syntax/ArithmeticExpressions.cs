using System;
using PiServer.version_2.interpreter.core;

namespace PiServer.version_2.interpreter.core.syntax
{
    public abstract class ArithmeticExpression
    {
        public abstract object Evaluate(PiEnvironment env);
    }

    public class NumberExpr : ArithmeticExpression
    {
        public int Value { get; }
        public NumberExpr(int value) => Value = value;
        public override object Evaluate(PiEnvironment env) => Value;
        public override string ToString() => Value.ToString();
    }

    public class VariableExpr : ArithmeticExpression
    {
        public string Name { get; }
        public VariableExpr(string name) => Name = name;

       public override object Evaluate(PiEnvironment env)
{
    if (env.Variables.TryGetValue(Name, out var val))
    {
        if (int.TryParse(val?.ToString(), out int num))
            return num;

        return val?.ToString() ?? "";
    }

    throw new Exception($"Unknown variable: {Name}");
}


        public override string ToString() => Name;
    }

    public class BinaryExpr : ArithmeticExpression
    {
        public string Op { get; }
        public ArithmeticExpression Left { get; }
        public ArithmeticExpression Right { get; }

        public BinaryExpr(string op, ArithmeticExpression left, ArithmeticExpression right)
        {
            Op = op;
            Left = left;
            Right = right;
        }

        public override object Evaluate(PiEnvironment env)
        {
            var lObj = Left.Evaluate(env);
            var rObj = Right.Evaluate(env);

            // поддержка чисел и строк
            if (lObj is int l && rObj is int r)
            {
                return Op switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" when r != 0 => l / r,
                    "/" => throw new DivideByZeroException($"Division by zero in expression: {Left} / {Right}"),
                    _ => throw new Exception($"Unknown operator: {Op}")
                };
            }
            else if (lObj is string ls && rObj is string rs && Op == "+")
            {
                return ls + rs;
            }

            throw new Exception($"Invalid operation '{Op}' between {lObj?.GetType().Name} and {rObj?.GetType().Name}");
        }

        public override string ToString() => $"({Left} {Op} {Right})";
    }
}
