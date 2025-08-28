using PiServer.version_2.interpreter.core;
using PiServer.version_2.interpreter.core.syntax;
using System.Text.Json.Serialization;
using PiServer.Services;


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
            Console.WriteLine($"=== ExecuteStepAsync started ===");
            Console.WriteLine($"Current process: {CurrentProcess}");
            Console.WriteLine($"Is completed: {IsCompleted}");

            try
            {
                if (IsCompleted)
                {
                    Console.WriteLine("Process already completed");
                    throw new InvalidOperationException("Process completed");
                }

                if (_env == null)
                {
                    Console.WriteLine("Environment is null");
                    throw new InvalidOperationException("Environment is not initialized");
                }

                var result = new StepResult
                {
                    CurrentState = string.Empty,
                    LastAction = string.Empty,
                    Variables = new Dictionary<string, string>(),
                    ChannelStates = new Dictionary<string, List<string>>(),
                    ActiveRestrictions = new List<string>()
                };

                // Обработка разных типов процессов
                if (CurrentProcess is OutputProcess op)
                {
                    Console.WriteLine($"Executing OutputProcess: {op}");
                    Console.WriteLine($"Channel: {op.Channel}, Message: {op.Message}");

                    await op.ExecuteAsync(_env);
                    _currentProcess = op.Continuation;
                    result.LastAction = $"Sent '{op.Message}' to {op.Channel}";
                }
                else if (CurrentProcess is InputProcess ip)
                {
                    Console.WriteLine($"Executing InputProcess: {ip}");
                    Console.WriteLine($"Channel: {ip.Channel}, Variable: {ip.Variable}");

                    await ip.ExecuteAsync(_env);
                    _currentProcess = ip.Continuation;
                    result.LastAction = $"Received on {ip.Channel}";
                }
                else if (CurrentProcess is ParallelProcess pp)
                {
                    Console.WriteLine($"Executing ParallelProcess with {pp.Processes.Count} processes");
                    var (newProcess, comms) = await ExecuteParallelCommunications(pp);
                    _currentProcess = newProcess;
                    result.LastAction = comms?.Count > 0 ? $"Parallel step ({comms.Count} actions)" : "No parallel actions";
                    result.ParallelActions = comms ?? new List<string>();
                }
                else if (CurrentProcess is NullProcess)
                {
                    Console.WriteLine("Executing NullProcess");
                    result.LastAction = "Null process";
                }
                else
                {
                    Console.WriteLine($"Unknown process type: {CurrentProcess.GetType()}");
                    throw new InvalidOperationException($"Unknown process type: {CurrentProcess.GetType()}");
                }

                result.CurrentState = CurrentProcess?.ToString() ?? "Null state";
                result.IsCompleted = IsCompleted;

                Console.WriteLine($"Step completed. Last action: {result.LastAction}");
                Console.WriteLine($"New state: {result.CurrentState}");
                Console.WriteLine($"=== ExecuteStepAsync completed ===");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ExecuteStepAsync ERROR ===");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner error: {ex.InnerException.Message}");
                }
                Console.WriteLine($"=== ExecuteStepAsync ERROR END ===");
                throw;
            }
        }

       

        private async Task<(Process NewProcess, List<string> Communications)> ExecuteParallelCommunications(ParallelProcess pp)
        {
            var continuations = new List<Process>();
            var communications = new List<string>();

            // Создаем КОПИИ списков для безопасной модификации
            var outputs = pp.Processes.OfType<OutputProcess>().ToList();
            var inputs = pp.Processes.OfType<InputProcess>().ToList();
            var lets = pp.Processes.OfType<LetProcess>().ToList();

            Console.WriteLine($"Parallel communications: {outputs.Count} outputs, {inputs.Count} inputs, {lets.Count} lets");

            // 1. Сначала выполняем все Let процессы
            foreach (var let in lets)
            {
                try
                {
                    Console.WriteLine($"Executing Let process: {let}");
                    await let.ExecuteAsync(_env);
                    continuations.Add(let.Continuation);
                    communications.Add($"Computed {let.ResultVar} = {let.Lambda}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Let process failed: {ex.Message}");
                    continuations.Add(let); // Оставляем как есть при ошибке
                }
            }

            // 2. Выполняем коммуникации между Output и Input процессами
            var matchedOutputs = new List<OutputProcess>();
            var matchedInputs = new List<InputProcess>();

            foreach (var output in outputs)
            {
                var matchingInput = inputs.FirstOrDefault(input =>
                    input.Channel == output.Channel &&
                    !matchedInputs.Contains(input));

                if (matchingInput != null)
                {
                    try
                    {
                        Console.WriteLine($"Found matching pair: {output.Channel}");

                        // Обрабатываем сообщение (возможно лямбда-выражение)
                        string message = output.Message;
                        if (IsLambdaExpression(message))
                        {
                            try
                            {
                                message = LambdaEvaluator.EvaluateLambda(message);
                                Console.WriteLine($"Lambda evaluated: {output.Message} -> {message}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Lambda evaluation failed: {ex.Message}");
                                // Используем оригинальное сообщение
                            }
                        }

                        // Отправляем сообщение
                        await _env.SendAsync(output.Channel, message);
                        communications.Add($"Sent '{message}' via {output.Channel}");

                        // Добавляем продолжения
                        continuations.Add(output.Continuation);
                        continuations.Add(Substitute(matchingInput.Continuation, matchingInput.Variable, message));

                        // Помечаем как обработанные
                        matchedOutputs.Add(output);
                        matchedInputs.Add(matchingInput);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Communication failed: {ex.Message}");
                        // Оставляем оба процесса для повторной попытки
                        continuations.Add(output);
                        continuations.Add(matchingInput);
                    }
                }
            }

            // 3. Добавляем необработанные процессы
            continuations.AddRange(outputs.Except(matchedOutputs));
            continuations.AddRange(inputs.Except(matchedInputs));

            Console.WriteLine($"Total continuations: {continuations.Count}");

            return (continuations.Count switch
            {
                0 => new NullProcess(),
                1 => continuations[0],
                _ => new ParallelProcess(continuations)
            }, communications);
        }


        private bool IsLambdaExpression(string expression)
        {
            return !string.IsNullOrEmpty(expression) &&
                   (expression.Contains("fun") ||
                    expression.Contains("->") ||
                    expression.Contains("λ") ||
                    expression.Contains("\\") ||
                    (expression.Contains('(') && expression.Contains(')')));
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


