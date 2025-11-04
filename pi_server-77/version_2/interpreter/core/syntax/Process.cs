using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiServer.Services;
using PiServer.version_2.interpreter.core;
using PiServer.version_2.interpreter.core.parser;

namespace PiServer.version_2.interpreter.core.syntax
{
    public abstract class Process
    {
        public abstract Task ExecuteAsync(PiEnvironment env);
    }

    public class IfElseProcess : Process
    {
        public Condition Condition { get; }
        public Process ThenBranch { get; }
        public Process ElseBranch { get; }

        public IfElseProcess(Condition condition, Process thenBranch, Process elseBranch)
        {
            Condition = condition;
            ThenBranch = thenBranch;
            ElseBranch = elseBranch;
        }

        public Process SelectBranch(PiEnvironment env)
        {
            bool result = Condition.Evaluate(env);
            Console.WriteLine($"[IfElseProcess] {Condition.Left} {Condition.Operator} {Condition.Right} => {result}");
            return result ? ThenBranch : ElseBranch;
        }

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            if (Condition.Evaluate(env))
                await ThenBranch.ExecuteAsync(env);
            else
                await ElseBranch.ExecuteAsync(env);
        }

        public override string ToString()
        {
            string op = Condition.Operator switch
            {
                TokenType.Equals => "==",
                TokenType.NotEquals => "!=",
                TokenType.GreaterThan => ">",
                TokenType.LessThan => "<",
                _ => Condition.Operator.ToString()
            };
            return $"if {Condition.Left} {op} {Condition.Right} then {ThenBranch} else {ElseBranch}";
        }
    }

    public class NullProcess : Process
    {
        public override Task ExecuteAsync(PiEnvironment env) => Task.CompletedTask;
        public override string ToString() => "0";
    }

    public class OutputProcess : Process
    {
        public string Channel { get; }
        public object Message { get; }      // теперь object — может быть string, ArithmeticExpression, LambdaTerm и т.д.
        public Process Continuation { get; }

        public OutputProcess(string channel, object message, Process continuation)
        {
            Channel = channel;
            Message = message;
            Continuation = continuation;
        }

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            // Получаем строковое представление сообщения с учётом типов
            string outputValue = ResolveMessageObject(Message, env);

            // Отправляем (PiEnvironment.SendAsync принимает object message в твоем текущем коде)
            await env.SendAsync(Channel, outputValue);

            // Продолжаем выполнение
            if (Continuation != null)
                await Continuation.ExecuteAsync(env);
        }

        public override string ToString() => $"{Channel}![{Message}].{Continuation}";

        // --- вспомогательное: приведение message -> строка, с безопасной обработкой арифметики и лямбда-выражений
        private string ResolveMessageObject(object? msgObj, PiEnvironment env)
        {
            if (msgObj == null) return "null";

            // 1) ArithmeticExpression — вычисляем и возвращаем число как строку
            if (msgObj is ArithmeticExpression aexpr)
            {
                try
                {
                    var eval = aexpr.Evaluate(env);
                    return eval?.ToString() ?? "null";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Arithmetic evaluation failed: {ex.Message}");
                    return aexpr.ToString();
                }
            }

            // 2) LambdaTerm — преобразуем в строку-выражение и попробуем вычислить через LambdaEvaluator
            if (msgObj is LambdaTerm lterm)
            {
                try
                {
                    // Используем представление лямбда-терма как текст и пытаемся его вычислить
                    string expr = lterm.ToString();
                    // если LambdaEvaluator ожидает формат типа "(fun x -> x+1) 5" — попытаться вычислить
                    string res = LambdaEvaluator.EvaluateLambda(expr);
                    return res ?? expr;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LambdaTerm evaluation failed: {ex.Message}");
                    return lterm.ToString();
                }
            }

            // 3) string — может быть: 
            //    - имя переменной (подставим значение из env),
            //    - лямбда-выражение в квадратных скобках "[...]" — вычислим содержимое,
            //    - простая строка (отправим как есть)
            if (msgObj is string s)
            {
                // 3a: если это формат [ ... ] — извлекаем содержимое и пробуем LambdaEvaluator
                var trimmed = s.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    try
                    {
                        var eval = LambdaEvaluator.EvaluateLambda(inner);
                        return eval ?? inner;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lambda evaluation (bracketed) failed: {ex.Message}");
                        // fallthrough -> пробуем как переменную
                    }
                }

                // 3b: попробовать получить значение переменной из окружения
                  try
                {
                    object? valObj = env.GetVariable(s);
                    if (valObj != null)
                    {
                        if (valObj is string strVal)
                            return strVal;
                        return valObj.ToString() ?? "null";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Variable lookup failed for '{s}': {ex.Message}");
                }

                // 3c: иначе — просто строка
                return s;
            }

            // 4) Любой другой тип — используем ToString(), безопасно
            return msgObj.ToString() ?? "null";
        }
    }

    public class InputProcess : Process
    {
        public string Channel { get; }
        public string Variable { get; }
        public Process Continuation { get; }

        public InputProcess(string channel, string variable, Process continuation)
        {
            Channel = channel;
            Variable = variable;
            Continuation = continuation;
        }

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            string message = (await env.ReceiveAsync(Channel))?.ToString() ?? string.Empty;
            env.SetVariable(Variable, message);
            if (Continuation != null)
                await Continuation.ExecuteAsync(env);
        }

        public override string ToString() => $"{Channel}?({Variable}).{Continuation}";
    }

    public class ParallelProcess : Process
    {
        public List<Process> Processes { get; }

        public ParallelProcess(List<Process> processes)
        {
            Processes = processes;
        }

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            var tasks = Processes.Select(p => p.ExecuteAsync(env));
            await Task.WhenAll(tasks);
        }

        public override string ToString() => string.Join(" | ", Processes);
    }

    public class RestrictionProcess : Process
    {
        public string Name { get; }
        public Process Body { get; }

        public RestrictionProcess(string name, Process body)
        {
            Name = name;
            Body = body;
        }

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            using (env.Restrict(Name))
            {
                env.SetVariable(Name, null);
                await Body.ExecuteAsync(env);
            }
        }

        public override string ToString() => $"(ν{Name}){Body}";
    }

    public class LetProcess : Process
    {
        public string ResultVar { get; }
        public LambdaTerm Lambda { get; }
        public string ArgumentVar { get; }
        public Process Continuation { get; }

        public LetProcess(string resultVar, LambdaTerm lambda, string argumentVar, Process continuation)
        {
            ResultVar = resultVar;
            Lambda = lambda;
            ArgumentVar = argumentVar;
            Continuation = continuation;
        }

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            // Получаем аргумент из переменной (может быть строкой/числом)
            string? argValue = env.GetVariable(ArgumentVar)?.ToString();
            // Lambda.Evaluate ожидает строковый аргумент в твоей реализации
            string result = Lambda.Evaluate(argValue);
            env.SetVariable(ResultVar, result);
            if (Continuation != null)
                await Continuation.ExecuteAsync(env);
        }

        public override string ToString() => $"let {ResultVar} = ({Lambda}) {ArgumentVar}.{Continuation}";
    }
}
