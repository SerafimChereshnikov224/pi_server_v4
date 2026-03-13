using PiServer.Services;
using PiServer.version_2.interpreter.core;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using System.Text.Json.Serialization;

namespace PiServer.version_2.runtime
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
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
            var result = new StepResult
            {
                CurrentState = string.Empty,
                LastAction = string.Empty,
                Variables = new Dictionary<string, string>(),
                ChannelStates = new Dictionary<string, List<string>>(),
                ActiveRestrictions = new List<string>()
            };

            if (IsCompleted) throw new InvalidOperationException("Process completed");

            // Обработка разных типов процессов
            if (CurrentProcess is OutputProcess op)
            {
                string msgStr = EvaluateMessageToString(op.Message);
                await op.ExecuteAsync(_env);
                _currentProcess = op.Continuation;
                result.LastAction = $"Sent '{msgStr}' to {op.Channel}";
            }
            else if (CurrentProcess is InputProcess ip)
            {
                await ip.ExecuteAsync(_env);
                _currentProcess = ip.Continuation;
                result.LastAction = $"Received on {ip.Channel}";
            }
            else if (CurrentProcess is ParallelProcess pp)
            {
                var (newProcess, comms) = await ExecuteParallelCommunications(pp);
                _currentProcess = newProcess;
                result.LastAction = comms?.Count > 0 ? $"Parallel step ({comms.Count} actions)" : "No parallel actions";
                result.ParallelActions = comms ?? new List<string>();
            }
            else if (CurrentProcess is IfElseProcess ifp)
            {
                bool conditionResult = EvaluateCondition(ifp.Condition, _env);
                _currentProcess = conditionResult ? ifp.ThenBranch : ifp.ElseBranch;
                result.LastAction = $"Condition evaluated to {conditionResult}";
            }
            else if (CurrentProcess is NullProcess)
            {
                result.LastAction = "Null process";
            }

            result.CurrentState = CurrentProcess?.ToString() ?? "Null state";
            result.IsCompleted = IsCompleted;

            result.Variables = _env.Variables.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
            foreach (var ch in _env.Channels.Keys)
            {
                result.ChannelStates[ch] = _env.GetChannelState(ch).ToList();
            }
            result.ActiveRestrictions = _env.ActiveRestrictions.ToList();

            return result;
        }

        private bool EvaluateCondition(Condition cond, PiEnvironment env) => cond.Evaluate(env);

        private Process UnwrapIfs(Process process)
        {
            if (process is IfElseProcess ifp)
            {
                try
                {
                    var branch = ifp.SelectBranch(_env);
                    return UnwrapIfs(branch);
                }
                catch
                {
                    return process;
                }
            }

            if (process is ParallelProcess pp)
            {
                var unwrapped = pp.Processes.Select(UnwrapIfs).ToList();
                return new ParallelProcess(unwrapped);
            }

            return process;
        }

        private async Task<(Process NewProcess, List<string> Communications)> ExecuteParallelCommunications(ParallelProcess pp)
        {
            var continuations = new List<Process>();
            var communications = new List<string>();

            var processes = pp.Processes.Select(UnwrapIfs).ToList();
            var outputs = processes.OfType<OutputProcess>().ToList();
            var inputs = processes.OfType<InputProcess>().ToList();
            var lets = processes.OfType<LetProcess>().ToList();

            foreach (var let in lets)
            {
                await let.ExecuteAsync(_env);
                continuations.Add(let.Continuation);
                communications.Add($"Computed {let.ResultVar} = {let.Lambda}");
            }

            var matchedOutputs = new List<OutputProcess>();
            var matchedInputs = new List<InputProcess>();

            foreach (var output in outputs)
            {
                if (output.IsBroadcast)
                {
                    var matchingInputs = inputs
                        .Where(input => input.Channel == output.Channel && !matchedInputs.Contains(input))
                        .ToList();

                    if (matchingInputs.Any())
                    {
                        string message = EvaluateMessageToString(output.Message);
                        message = SubstituteVariablesInLambda(message, _env);
                        await _env.SendAsync(output.Channel, message, true); // broadcast

                        foreach (var input in matchingInputs)
                        {
                            _env.SetVariable(input.Variable, message);
                            continuations.Add(input.Continuation);
                            matchedInputs.Add(input);
                            communications.Add($"Broadcast '{message}' to {input.Channel}?({input.Variable})");
                        }

                        continuations.Add(output.Continuation);
                        matchedOutputs.Add(output);
                    }
                }
                else // unicast
                {
                    var matchingInput = inputs.FirstOrDefault(input =>
                        input.Channel == output.Channel && !matchedInputs.Contains(input));

                    if (matchingInput != null)
                    {
                        string message = EvaluateMessageToString(output.Message);
                        message = SubstituteVariablesInLambda(message, _env);
                        await _env.SendAsync(output.Channel, message, false);

                        _env.SetVariable(matchingInput.Variable, message);

                        continuations.Add(output.Continuation);
                        continuations.Add(matchingInput.Continuation);

                        communications.Add($"Sent '{message}' via {output.Channel}");

                        matchedOutputs.Add(output);
                        matchedInputs.Add(matchingInput);
                    }
                }
            }

            continuations.AddRange(outputs.Except(matchedOutputs));
            continuations.AddRange(inputs.Except(matchedInputs));
            continuations.AddRange(processes.Except(outputs).Except(inputs).Except(lets));

            continuations = continuations.Where(p => p is not NullProcess).ToList();

            if (continuations.Count == 0) return (new NullProcess(), communications);
            if (continuations.Count == 1) return (continuations[0], communications);

            return (new ParallelProcess(continuations), communications);
        }

        private bool IsLambdaExpression(string expression)
        {
            return !string.IsNullOrEmpty(expression) &&
                   (expression.Contains("fun") || expression.Contains("->") || expression.Contains("λ") ||
                    expression.Contains("\\") || (expression.Contains('(') && expression.Contains(')')));
        }

        private string SubstituteVariablesInLambda(string expr, PiEnvironment env)
        {
            foreach (var kv in env.Variables)
            {
                var name = kv.Key;
                var val = kv.Value?.ToString() ?? "null";
                expr = Regex.Replace(expr, $@"\b{Regex.Escape(name)}\b", val);
            }
            return expr;
        }

        private string EvaluateMessageToString(object messageObj)
        {
            if (messageObj == null) return "";

            if (messageObj is string s)
            {
                if (IsLambdaExpression(s))
                {
                    s = SubstituteVariablesInLambda(s, _env);
                    try { return LambdaEvaluator.EvaluateLambda(s); }
                    catch { return s; }
                }
                return s;
            }

            if (messageObj is ArithmeticExpression ae)
            {
                try { return ae.Evaluate(_env).ToString(); }
                catch { return ae.ToString(); }
            }

            if (messageObj is LambdaTerm lt)
            {
                var asStr = lt.ToString();
                asStr = SubstituteVariablesInLambda(asStr, _env);
                try { return LambdaEvaluator.EvaluateLambda(asStr); }
                catch { return asStr; }
            }

            return messageObj.ToString() ?? "";
        }
        public string SubstituteVariablesInExpression(string expr, PiEnvironment env)
        {
            if (string.IsNullOrEmpty(expr) || env == null)
                return expr;

            foreach (var kv in env.Variables)
            {
                var name = kv.Key;
                var value = kv.Value?.ToString() ?? "null";

                // Заменяем только полные совпадения имени переменной
                expr = System.Text.RegularExpressions.Regex.Replace(expr,
                    $@"\b{name}\b", value);
            }
            return expr;
        }
        
        public static string SubstituteVariablesInExpressionStatic(string expr, PiEnvironment env)
        {
            if (string.IsNullOrEmpty(expr) || env == null)
                return expr;

            foreach (var kv in env.Variables)
            {
                var name = kv.Key;
                var value = kv.Value?.ToString() ?? "null";
                expr = Regex.Replace(expr, $@"\b{name}\b", value);
            }

            return expr;
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
