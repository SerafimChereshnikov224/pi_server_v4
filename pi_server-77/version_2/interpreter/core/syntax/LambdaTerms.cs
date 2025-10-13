using System;

namespace PiServer.version_2.interpreter.core.syntax
{
    public abstract class LambdaTerm
    {
        public abstract override string ToString();

        public abstract string Evaluate(string argument);
    }

    public class LambdaVar : LambdaTerm
    {
        public string Name { get; }
        public LambdaVar(string name) => Name = name;

        public override string ToString() => Name;

        public override string Evaluate(string argument)
        {
            return argument;
        }
    }

    public class LambdaAbs : LambdaTerm
    {
        public string Param { get; }
        public LambdaTerm Body { get; }

        public LambdaAbs(String param, LambdaTerm body)
        {
            Param = param;
            Body = body;
        }

        public override string Evaluate(string argument)
        {
            return Body.Evaluate(argument);
        }
        public override string ToString() => $"λ{Param}.{Body}";
    }

    public class LambdaApp : LambdaTerm
    {
        public LambdaTerm Func { get; }
        public LambdaTerm Arg { get; }

        public LambdaApp(LambdaTerm func, LambdaTerm arg)
        {
            Func = func;
            Arg = arg;
        }

        public override string ToString() => $"({Func} {Arg})";

        public override string Evaluate(string argument)
        {
            string funcResult = Func.Evaluate(argument);
            string argResult = Arg.Evaluate(argument);

            if (Func is LambdaAbs abs)
            {
                return abs.Body.Evaluate(argResult);
            }

            return $"{funcResult} {argResult}";
        }
    }
}