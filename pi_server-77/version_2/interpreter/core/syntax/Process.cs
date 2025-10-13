using System;
using PiServer.Services;
using System.Text;
using PiServer.version_2.interpreter.core;


namespace PiServer.version_2.interpreter.core.syntax
{
    public abstract class Process
    {
        public abstract Task ExecuteAsync(PiEnvironment env);
    }

    public class NullProcess : Process
    {
        public override Task ExecuteAsync(PiEnvironment env) => Task.CompletedTask;
        public override string ToString() => "0";
    }

    public class OutputProcess : Process
    {
        public string Channel { get; }
        public string Message { get; }
        public Process Continuation { get; }

        public OutputProcess(string channel, string message, Process continuation)
        {
            Channel = channel;
            Message = message;
            Continuation = continuation;
        }



        public override async Task ExecuteAsync(PiEnvironment env)
        {
            Console.WriteLine($"*** OutputProcess.ExecuteAsync started ***");
            Console.WriteLine($"Original message: '{Message}'");

            string processedMessage = Message;

            // Проверяем, является ли сообщение лямбда-выражением
            if (string.IsNullOrEmpty(Message))
            {
                processedMessage = "null"; // или любое значение по умолчанию
            }
            else if (IsLambdaExpression(Message))
            {
                try
                {
                    Console.WriteLine($"Detected lambda expression, evaluating...");
                    processedMessage = LambdaEvaluator.EvaluateLambda(Message);
                    Console.WriteLine($"Lambda evaluation result: '{processedMessage}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lambda evaluation failed: {ex.Message}");
                    // Пытаемся получить значение переменной
                    try
                    {
                        processedMessage = env.GetVariable(Message) ?? Message;
                        Console.WriteLine($"Using variable value: '{processedMessage}'");
                    }
                    catch
                    {
                        processedMessage = Message;
                        Console.WriteLine($"Using original message: '{processedMessage}'");
                    }
                }
            }
            else
            {
                // Обычное сообщение
                try
                {
                    processedMessage = env.GetVariable(Message) ?? Message;
                    Console.WriteLine($"Using variable value: '{processedMessage}'");
                }
                catch
                {
                    processedMessage = Message;
                    Console.WriteLine($"Using original message: '{processedMessage}'");
                }
            }

            Console.WriteLine($"Sending message: '{processedMessage}' via channel '{Channel}'");

            try
            {
                await env.SendAsync(Channel, processedMessage);
                Console.WriteLine($"Message sent successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
                throw;
            }

            Console.WriteLine($"Executing continuation: {Continuation}");
            await Continuation.ExecuteAsync(env);

            Console.WriteLine($"*** OutputProcess.ExecuteAsync completed ***");
        }

private bool IsLambdaExpression(string message)
{
    // Проверяем признаки лямбда-выражения
    return !string.IsNullOrEmpty(message) && 
           (message.Contains("fun") || 
            message.Contains("->") || 
            message.Contains("λ") || 
            message.Contains("\\") ||
            (message.Contains('(') && message.Contains(')')));
}

        public override string ToString() => $"{Channel}![{Message}].{Continuation}";
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
            Console.WriteLine($"*** InputProcess.ExecuteAsync started ***");
            Console.WriteLine($"Waiting for message on channel: '{Channel}'");

            try
            {
                string message = await env.ReceiveAsync(Channel);
                Console.WriteLine($"Received message: '{message}'");

                // Сохраняем в переменную
                env.SetVariable(Variable, message);
                Console.WriteLine($"Variable '{Variable}' set to: '{message}'");

                Console.WriteLine($"Executing continuation: {Continuation}");
                await Continuation.ExecuteAsync(env);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InputProcess: {ex.Message}");
                throw;
            }

            Console.WriteLine($"*** InputProcess.ExecuteAsync completed ***");
        }



        private string EvaluateLambda(string expression)
        {
            try
            {
                // Вместо упрощенной логики вызываем ваш мощный вычислитель
                return LambdaEvaluator.EvaluateLambda(expression);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но возвращаем оригинальное выражение
                Console.WriteLine($"Lambda evaluation failed: {ex.Message}");
                return expression;
            }
        }

        private Process Substitute(Process process, string variable, string value)
        {
            // Простая реализация подстановки
            if (process is NullProcess) return process;
            if (process is OutputProcess op)
                return new OutputProcess(
                    op.Channel == variable ? value : op.Channel,
                    op.Message == variable ? value : op.Message,
                    Substitute(op.Continuation, variable, value)
                );
            if (process is InputProcess ip)
                return new InputProcess(
                    ip.Channel == variable ? value : ip.Channel,
                    ip.Variable, // Не подставляем в связанные переменные
                    Substitute(ip.Continuation, variable, value)
                );
            return process;
        }

        public override string ToString() => $"{Channel}?({Variable}).{Continuation}";
    }

    public class ParallelProcess : Process
    {
        public List<Process> Processes { get; }

        public ParallelProcess(List<Process> processes) => Processes = processes.ToList();

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            var tasks = Processes.Select(p => p.ExecuteAsync(env)).ToArray();
            await Task.WhenAll(tasks);
        }

        public override string ToString() => $"({string.Join(" | ", Processes)})";
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
        public string ResultVar { get; }     // Куда сохранить результат (z)
        public LambdaTerm Lambda { get; }   // λ-терм (λx.x)
        public string ArgumentVar { get; }   // Какая переменная подставляется (x)
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
            string argValue = env.GetVariable(ArgumentVar); // Получаем "hello" для x
            string result = Lambda.Evaluate(argValue);     // Вычисляем (λx.x) "hello" → "hello"
            env.SetVariable(ResultVar, result);            // Сохраняем z = "hello"
            await Continuation.ExecuteAsync(env);
        }

        public override string ToString() => $"let {ResultVar} = ({Lambda}) {ArgumentVar}.{Continuation}";
    }


    public static class LambdaMessageProcessor
    {
        public static string ProcessLambdaInMessage(string message, PiEnvironment env)
        {
            // Проверяем, содержит ли сообщение лямбда-выражение
            if (IsLambdaExpression(message))
            {
                try
                {
                    // Используем ваш мощный вычислитель
                    return LambdaEvaluator.EvaluateLambda(message);
                }
                catch
                {
                    // Если не удалось вычислить, возвращаем как есть
                    return message;
                }
            }

            // Пытаемся получить значение переменной
            try
            {
                return env.GetVariable(message) ?? message;
            }
            catch
            {
                return message;
            }
        }



        private static bool IsLambdaExpression(string expression)
        {
            return expression.Contains("λ") ||
                   expression.Contains("fun") ||
                   expression.Contains("->") ||
                   (expression.Contains('(') && expression.Contains(')'));
        }
    



        private static string ExtractLambdaExpression(string message)
        {
            // Ищем начало лямбда-выражения
            int lambdaIndex = message.IndexOf("λ", StringComparison.Ordinal);
            if (lambdaIndex == -1)
                lambdaIndex = message.IndexOf("\\", StringComparison.Ordinal);
            if (lambdaIndex == -1) return message;

            // Извлекаем лямбда-выражение
            var sb = new StringBuilder();
            int parenDepth = 0;
            bool inLambda = false;

            for (int i = lambdaIndex; i < message.Length; i++)
            {
                char c = message[i];

                if (c == 'λ' || c == '\\')
                {
                    inLambda = true;
                    sb.Append('λ');
                    continue;
                }

                if (inLambda)
                {
                    if (c == '(') parenDepth++;
                    if (c == ')') parenDepth--;

                    sb.Append(c);

                    // Завершаем, когда достигли конца выражения
                    if (parenDepth == 0 && (c == ' ' || i == message.Length - 1))
                    {
                        break;
                    }
                }
            }

            return sb.ToString();
        }
    }
}
