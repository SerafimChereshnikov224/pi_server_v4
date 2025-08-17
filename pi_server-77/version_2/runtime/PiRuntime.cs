using PiServer.version_2.interpreter.core;
using PiServer.version_2.interpreter.core.syntax;
using System.Text.Json.Serialization;

namespace PiServer.version_2.runtime
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
        public class PiRuntime
        {
            public readonly PiEnvironment _env = new();
            private Process _currentProcess;

            public Process CurrentProcess => _currentProcess;

        public bool IsCompleted => _currentProcess switch
        {
            NullProcess => true,
            ParallelProcess pp => pp.Processes.All(p => p is NullProcess),
            _ => false
        };

        public PiRuntime(Process initialProcess)
            {
                _currentProcess = initialProcess;
            }

        public async Task<StepResult> ExecuteStepAsync()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Process completed");

            if (_env == null)
                throw new InvalidOperationException("Environment is not initialized");

            var result = new StepResult
            {
                // Инициализируем обязательные свойства
                CurrentState = string.Empty,
                LastAction = string.Empty,
                Variables = new Dictionary<string, string>(),
                ChannelStates = new Dictionary<string, List<string>>(),
                ActiveRestrictions = new List<string>()
            };

            if (_currentProcess is ParallelProcess pp)
            {
                var (newProcess, comms) = await ExecuteParallelCommunications(pp);
                _currentProcess = newProcess;
                result.ParallelActions = comms ?? new List<string>();
                result.LastAction = comms?.Count > 0 ? $"Parallel step ({comms.Count} actions)" : "No parallel actions";
            }
            else
            {
                result.LastAction = GetActionType(_currentProcess) ?? "Unknown action";
                await _currentProcess.ExecuteAsync(_env);
                _currentProcess = GetNextProcess(_currentProcess) ?? throw new InvalidOperationException("Next process is null");
            }

            // Заполняем состояние процесса
            result.CurrentState = _currentProcess?.ToString() ?? "Null state";
            result.IsCompleted = IsCompleted;

            // Заполняем окружение из PiEnvironment
            if (_env.Variables != null)
            {
                result.Variables = _env.Variables.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                );
            }

            if (_env.Channels != null)
            {
                result.ChannelStates = _env.Channels.ToDictionary(
                    channel => channel.Key,
                    channel => new PiServer.version_2.runtime.Channel("temp").GetMessages()?.ToList() ?? new List<string>()
                );
            }

            // Предполагая, что добавили это свойство в PiEnvironment
            if (_env.ActiveRestrictions != null)
            {
                result.ActiveRestrictions = _env.ActiveRestrictions.ToList();
            }

            return result;
        }

        private async Task<(Process NewProcess, List<string> Communications)> ExecuteParallelCommunications(ParallelProcess pp)
        {
            var continuations = new List<Process>();
            var communications = new List<string>();

            // Группируем процессы по типам
            var lets = pp.Processes.OfType<LetProcess>().ToList();

            var outputs = pp.Processes
                .Where(p => p is OutputProcess && !(p is LetProcess))
                .Cast<OutputProcess>()
                .ToList();

            var inputs = pp.Processes
                .Where(p => p is InputProcess && !(p is LetProcess))
                .Cast<InputProcess>()
                .ToList();

            // 1. Выполняем все возможные коммуникации
            foreach (var op in outputs.ToList())
            {
                var matchingInput = inputs.FirstOrDefault(ip => ip.Channel == op.Channel);
                if (matchingInput != null)
                {
                    string message = _env.GetVariable(op.Message);
                    await _env.SendAsync(op.Channel, message);
                    communications.Add($"Sent '{message}' via {op.Channel}");

                    continuations.Add(Substitute(matchingInput.Continuation, matchingInput.Variable, message));
                    continuations.Add(op.Continuation);

                    outputs.Remove(op);
                    inputs.Remove(matchingInput);
                }
            }

            foreach (var let in lets)
            {
                await let.ExecuteAsync(_env);
                continuations.Add(let.Continuation);
                communications.Add($"Computed {let.ResultVar} = {let.Lambda}");
            }

            continuations.AddRange(outputs);
            continuations.AddRange(inputs);

            return (continuations.Count switch
            {
                0 => new NullProcess(),
                1 => continuations[0],
                _ => new ParallelProcess(continuations)
            }, communications);
        }

        private Process Substitute(Process process, string variable, string value)
            {
                if (process is NullProcess) return process;
                if (process is OutputProcess op)
                    return new OutputProcess(
                        op.Channel == variable ? value : op.Channel,
                        op.Message == variable ? value : op.Message,
                        Substitute(op.Continuation, variable, value));

                if (process is InputProcess ip)
                    return new InputProcess(
                        ip.Channel == variable ? value : ip.Channel,
                        ip.Variable,
                        Substitute(ip.Continuation, variable, value));
                if (process is LetProcess lp)
                    return new LetProcess(
                    lp.ResultVar,
                    lp.Lambda,
                    lp.ArgumentVar == variable ? value : lp.ArgumentVar, 
                    Substitute(lp.Continuation, variable, value)
                );

            return process;
            }

        private string GetActionType(Process process)
        {
            return process switch
            {
                OutputProcess op => $"Sent '{GetMessageValue(op)}' to {op.Channel}",
                InputProcess ip => $"Received on {ip.Channel}",
                RestrictionProcess rp => $"New restriction '{rp.Name}'",
                _ => "Process advanced"
            };
        }

        private Process GetNextProcess(Process process)
            {
                return process switch
                {
                    OutputProcess op => op.Continuation,
                    InputProcess ip => ip.Continuation,
                    LetProcess lp => lp.Continuation,
                    _ => process
                };
            }

        private string GetMessageValue(OutputProcess op)
        {
            try
            {
                return _env.GetVariable(op.Message);
            }
            catch
            {
                return op.Message;
            }
        }
    }

    public class StepResult
    {
        public string CurrentState { get; set; } = string.Empty;
        public string LastAction { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public List<string> ParallelActions { get; set; } = new();
        public Dictionary<string, string> Variables { get; set; } = new();
        public Dictionary<string, List<string>> ChannelStates { get; set; } = new();
        public List<string> ActiveRestrictions { get; set; } = new();
    }
}


