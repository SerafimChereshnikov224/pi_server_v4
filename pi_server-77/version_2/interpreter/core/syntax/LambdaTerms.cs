namespace PiServer.version_2.interpreter.core.syntax
{
    public abstract class LambdaTerm
    {
        public abstract override string ToString();
    }

    public class LambdaVar : LambdaTerm
    {
        public string Name { get; }
        public LambdaVar(string name) => Name = name;
        public override string ToString() => Name;
    }

    public class LambdaAbs : LambdaTerm
    {
        public string Param { get; }
        public LambdaTerm Body { get; }
        public LambdaAbs(string param, LambdaTerm body)
        {
            Param = param;
            Body = body;
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
    }
}