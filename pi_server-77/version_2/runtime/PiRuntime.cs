using PiServer.Services;
using PiServer.version_2.interpreter.core;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using System.Text.Json.Serialization;

namespace PiServer.version_2.runtime
{
    using System.Collections.Generic;
    using Process = PiServer.version_2.interpreter.core.syntax.Process;
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
            if (CurrentProcess is AgentProcess ap)
            {
                _currentProcess = ap.InnerProcess;

                result.LastAction = $"Entered agent '{ap.AgentName}'";
            }
            else if (CurrentProcess is OutputProcess op)
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

            // Проверка на deadlock (только если процесс ещё не завершён)
            if (!result.IsCompleted)
            {
                result.IsDeadlocked = IsDeadlocked();
                if (result.IsDeadlocked)
                {
                    var deadInputs = CollectInputs(_currentProcess);
                    result.DeadlockedInputs = deadInputs.Select(ip => ip.ToString()).ToList();
                }
            }

            return result;
        }

        private bool EvaluateCondition(Condition cond, PiEnvironment env) => cond.Evaluate(env);

        private Process UnwrapIfs(Process process)
        {
            if (process is AgentProcess ap)
            {
                return new AgentProcess(
                    ap.AgentName,
                    UnwrapIfs(ap.InnerProcess)
                );
            }

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

        public List<InputProcess> CollectInputs(Process process)
        {
            var result = new List<InputProcess>();
            CollectInputsInternal(process, result);
            return result;
        }

        public List<OutputProcess> CollectOutputs(Process process)
        {
            var result = new List<OutputProcess>();
            CollectOutputsInternal(process, result);
            return result;
        }

        private void CollectInputsInternal(Process process, List<InputProcess> result)
        {
            switch (process)
            {
                case InputProcess ip:
                    result.Add(ip);
                    break;
                case OutputProcess op:
                    CollectInputsInternal(op.Continuation, result);
                    break;
                case ParallelProcess pp:
                    foreach (var p in pp.Processes)
                        CollectInputsInternal(p, result);
                    break;
                case IfElseProcess ifp:
                    CollectInputsInternal(ifp.ThenBranch, result);
                    CollectInputsInternal(ifp.ElseBranch, result);
                    break;
                case LetProcess lp:
                    CollectInputsInternal(lp.Continuation, result);
                    break;
                // RestrictionProcess игнорируем по просьбе, но если встретится, обойдём тело
                case RestrictionProcess rp:
                    CollectInputsInternal(rp.Body, result);
                    break;
                    // NullProcess и прочее игнорируем
            }
        }

        private void CollectOutputsInternal(Process process, List<OutputProcess> result)
        {
            switch (process)
            {
                case OutputProcess op:
                    result.Add(op);
                    break;
                case InputProcess ip:
                    CollectOutputsInternal(ip.Continuation, result);
                    break;
                case ParallelProcess pp:
                    foreach (var p in pp.Processes)
                        CollectOutputsInternal(p, result);
                    break;
                case IfElseProcess ifp:
                    CollectOutputsInternal(ifp.ThenBranch, result);
                    CollectOutputsInternal(ifp.ElseBranch, result);
                    break;
                case LetProcess lp:
                    CollectOutputsInternal(lp.Continuation, result);
                    break;
                case RestrictionProcess rp:
                    CollectOutputsInternal(rp.Body, result);
                    break;
            }
        }

        private Process Substitute(Process process, string variable, string value)
        {
            if (process is NullProcess) return process;
            if (process is OutputProcess op)
            {
                string newMessage = op.Message?.ToString();
                if (newMessage != null && newMessage.Contains(variable))
                {
                    newMessage = ReplaceInString(newMessage, variable, value);
                }
                return new OutputProcess(
                    op.Channel == variable ? value : op.Channel,
                    newMessage,
                    Substitute(op.Continuation, variable, value),
                    op.IsBroadcast);
            }
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
                    Substitute(lp.Continuation, variable, value));
            if (process is ParallelProcess pp)
            {
                var substituted = pp.Processes.Select(p => Substitute(p, variable, value)).ToList();
                return new ParallelProcess(substituted);
            }
            if (process is IfElseProcess ifp)
            {
                // Подставляем переменную в условие (левая и правая части)
                var newCondition = new Condition(
                    SubstituteArithmetic(ifp.Condition.Left, variable, value),
                    ifp.Condition.Operator,
                    SubstituteArithmetic(ifp.Condition.Right, variable, value)
                );
                // Подставляем переменную в обе ветки (рекурсивно)
                var newThen = Substitute(ifp.ThenBranch, variable, value);
                var newElse = Substitute(ifp.ElseBranch, variable, value);
                // Возвращаем новый IfElseProcess (не вычисляем условие, оставляем на следующий шаг)
                return new IfElseProcess(newCondition, newThen, newElse);
            }
            if (process is RestrictionProcess rp)
                return new RestrictionProcess(rp.Name, Substitute(rp.Body, variable, value));
            return process;
        }

        private ArithmeticExpression SubstituteArithmetic(ArithmeticExpression expr, string variable, string value)
        {
            if (expr is VariableExpr varExpr && varExpr.Name == variable)
                return new NumberExpr(int.Parse(value));
            if (expr is BinaryExpr binExpr)
                return new BinaryExpr(
                    binExpr.Op,
                    SubstituteArithmetic(binExpr.Left, variable, value),
                    SubstituteArithmetic(binExpr.Right, variable, value)
                );
            return expr;
        }

        public bool IsDeadlocked()
        {
            // Если процесс завершён, deadlock быть не может
            if (IsCompleted) return false;

            var inputs = CollectInputs(_currentProcess);
            if (inputs.Count == 0) return false; // нет входов -> всегда есть возможность выполнить выходы

            var outputs = CollectOutputs(_currentProcess);
            if (outputs.Count > 0) return false; // есть выходы -> они могут выполниться

            // Проверяем, есть ли сообщения в каналах для ожидающих входов
            foreach (var input in inputs)
            {
                var channel = _env.GetChannel(input.Channel);
                if (channel.HasMessages())
                    return false; // хотя бы один вход может получить сообщение
            }

            // Все входы ждут, каналы пусты
            return true;
        }

        private async Task<(Process NewProcess, List<string> Communications)> ExecuteParallelCommunications(ParallelProcess pp)
        {
            var continuations = new List<Process>();
            var communications = new List<string>();

            var processes = pp.Processes
                .Select(UnwrapIfs)
                .Select(p => p is AgentProcess ap ? ap.InnerProcess : p)
                .ToList();            
            var outputs = processes.OfType<OutputProcess>().ToList();
            var inputs = processes.OfType<InputProcess>().ToList();
            var lets = processes.OfType<LetProcess>().ToList();

            // Let-выражения
            foreach (var let in lets)
            {
                await let.ExecuteAsync(_env);
                continuations.Add(let.Continuation);
                communications.Add($"Computed {let.ResultVar} = {let.Lambda}");
            }

            var matchedOutputs = new List<OutputProcess>();
            var matchedInputs = new List<InputProcess>();

            // Обработка пар (коммуникаций)
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

                        foreach (var input in matchingInputs)
                        {
                            var substitutedContinuation = Substitute(input.Continuation, input.Variable, message);
                            continuations.Add(substitutedContinuation);
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

                        var substitutedContinuation = Substitute(matchingInput.Continuation, matchingInput.Variable, message);
                        continuations.Add(output.Continuation);
                        continuations.Add(substitutedContinuation);
                        communications.Add($"Sent '{message}' via {output.Channel}");

                        matchedOutputs.Add(output);
                        matchedInputs.Add(matchingInput);
                    }
                }
            }

            // Если коммуникаций не было, выполняем одиночные выходы и входы с сообщениями
            if (!matchedOutputs.Any() && !matchedInputs.Any())
            {
                foreach (var output in outputs)
                {
                    await output.ExecuteAsync(_env);
                    continuations.Add(output.Continuation);
                    communications.Add($"Executed output on {output.Channel} (no partner)");
                    matchedOutputs.Add(output);
                }

                foreach (var input in inputs)
                {
                    var channel = _env.GetChannel(input.Channel);
                    if (channel.HasMessages())
                    {
                        await input.ExecuteAsync(_env);
                        continuations.Add(input.Continuation);
                        communications.Add($"Executed input on {input.Channel} (message available)");
                        matchedInputs.Add(input);
                    }
                }
            }

            // Добавляем неиспользованные процессы
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

        private string ReplaceInString(string expr, string variable, string value)
        {
            return Regex.Replace(expr, $@"\b{Regex.Escape(variable)}\b", value);
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
        public bool IsDeadlocked { get; set; }
        public List<string> DeadlockedInputs { get; set; } = new();
    }
}
