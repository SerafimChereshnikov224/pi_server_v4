using System;

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
            await env.SendAsync(Channel, Message);
            await Continuation.ExecuteAsync(env);
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
            var message = await env.ReceiveAsync(Channel);
            var substituted = Substitute(Continuation, Variable, message);
            await substituted.ExecuteAsync(env);
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
                await Body.ExecuteAsync(env);
            }
        }

        public override string ToString() => $"(ν{Name}){Body}";
    }
}
