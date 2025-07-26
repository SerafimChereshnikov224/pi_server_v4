using PiServer.version_2.interpreter.core;
using PiServer.version_2.interpreter.core.syntax;

namespace PiServer.version_2.runtime
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
        public class PiRuntime
        {
            private readonly PiEnvironment _env = new();
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

                var result = new StepResult();

                if (_currentProcess is ParallelProcess pp)
                {
                    // Параллельное выполнение всех возможных коммуникаций
                    var executed = await ExecuteParallelCommunications(pp);

                    result.CurrentState = executed.NewProcess.ToString();
                    result.LastAction = $"Parallel step ({executed.Communications.Count} actions)";
                    result.ParallelActions = executed.Communications;
                    _currentProcess = executed.NewProcess;
                }
                else
                {
                    // Последовательное выполнение
                    result.LastAction = GetActionType(_currentProcess);
                    await _currentProcess.ExecuteAsync(_env);
                    _currentProcess = GetNextProcess(_currentProcess);
                    result.CurrentState = _currentProcess.ToString();
                }

                result.IsCompleted = IsCompleted;
                return result;
            }

            private async Task<(Process NewProcess, List<string> Communications)> ExecuteParallelCommunications(ParallelProcess pp)
            {
                var remaining = new List<Process>();
                var communications = new List<string>();

                // 1. Выполняем все возможные коммуникации
                var outputs = pp.Processes.OfType<OutputProcess>().ToList();
                var inputs = pp.Processes.OfType<InputProcess>().ToList();

                foreach (var op in outputs)
                {
                    var matchingInput = inputs.FirstOrDefault(ip => ip.Channel == op.Channel);
                    if (matchingInput != null)
                    {
                        // Выполняем коммуникацию
                        await _env.SendAsync(op.Channel, op.Message);
                        communications.Add($"Sent '{op.Message}' via {op.Channel}");

                        // Подставляем значение и сохраняем продолжения
                        var subContinuation = Substitute(matchingInput.Continuation, matchingInput.Variable, op.Message);
                        remaining.Add(subContinuation);
                        remaining.Add(op.Continuation);

                        // Помечаем как обработанные
                        inputs.Remove(matchingInput);
                        continue;
                    }

                    // Если получателя нет, оставляем как есть
                    remaining.Add(op);
                }

                // 2. Добавляем оставшиеся процессы
                remaining.AddRange(inputs);
                remaining.AddRange(pp.Processes.Where(p => p is not OutputProcess and not InputProcess));

                // 3. Формируем новый процесс
                var newProcess = remaining.Count switch
                {
                    0 => new NullProcess(),
                    1 => remaining[0],
                    _ => new ParallelProcess(remaining)
                };

                return (newProcess, communications);
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

                return process;
            }

            private string GetActionType(Process process)
            {
                return process switch
                {
                    OutputProcess op => $"Sent '{op.Message}' to {op.Channel}",
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
                    _ => process
                };
            }
}

        public class StepResult
        {
            public string CurrentState { get; set; }
            public string LastAction { get; set; }
            public bool IsCompleted { get; set; }
            public List<string> ParallelActions { get; set; } = new List<string>();
        }
    }


