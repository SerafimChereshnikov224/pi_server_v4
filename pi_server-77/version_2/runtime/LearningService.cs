using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.models;
using PiServer.Services;
using PiServer.version_2.runtime;
using System.Collections.Generic;
using System.Linq;
using PiServer.version_2.interpreter.core;
using System.Text.RegularExpressions;

namespace PiServer.version_2.runtime
{
    public class LearningService
    {
        private readonly List<ReductionStep> _reductionHistory = new();
        private string _expectedNextStep;

        public LearningService() { }

        public string GetCurrentExpectedStep() => _expectedNextStep;

        public void CalculateExpectedNextStep(Process currentProcess, PiEnvironment env, bool isCompleted)
        {
            if (isCompleted)
            {
                _expectedNextStep = "0";
                return;
            }

            var nextProcess = SimulateFullStep(currentProcess, env);

            // Подставляем значения переменных только в строковом представлении
            _expectedNextStep = nextProcess != null
                ? PiRuntime.SubstituteVariablesInExpressionStatic(nextProcess.ToString(), env)
                : "0";
        }

        private Process SimulateFullStep(Process process, PiEnvironment env)
        {
            switch (process)
            {
                case ParallelProcess pp:
                    return SimulateParallelFullStep(pp, env);
                case OutputProcess op when IsLambdaExpression(op.Message):
                    return SimulateOutputWithLambda(op, env);
                case IfElseProcess ifp:
                    bool cond = ifp.Condition.Evaluate(env);
                    return cond ? ifp.ThenBranch : ifp.ElseBranch;
                case LetProcess lp:
                    return lp; // не трогаем LambdaTerm
                default:
                    return GetNextProcess(process);
            }
        }

        private Process SimulateParallelFullStep(ParallelProcess pp, PiEnvironment env)
        {
            var processesAfterEval = pp.Processes.Select(p =>
            {
                if (p is OutputProcess op && IsLambdaExpression(op.Message))
                {
                    var evaluatedMessage = TryEvaluateLambda(op.Message, env);
                    return new OutputProcess(op.Channel, evaluatedMessage, op.Continuation, op.IsBroadcast);
                }
                return p;
            }).ToList();

            return SimulateCommunication(processesAfterEval, env);
        }

        private Process SimulateCommunication(List<Process> processes, PiEnvironment env)
        {
            var outputs = processes.OfType<OutputProcess>().ToList();
            var inputs = processes.OfType<InputProcess>().ToList();
            var lets = processes.OfType<LetProcess>().ToList();

            var continuations = new List<Process>();
            foreach (var let in lets)
            {
                // Подставляем значение переменной в окружение, LambdaTerm не меняем
                var evaluated = TryEvaluateLambda(let.Lambda, env);
                env.SetVariable(let.ResultVar, evaluated);
                continuations.Add(let.Continuation);
            }

            var matchedOutputs = new List<OutputProcess>();
            var matchedInputs = new List<InputProcess>();

            foreach (var output in outputs)
            {
                var matchingInput = inputs.FirstOrDefault(input =>
                    input.Channel == output.Channel && !matchedInputs.Contains(input));

                if (matchingInput != null)
                {
                    var msgStr = output.Message?.ToString() ?? "";
                    msgStr = SubstituteVariablesInLambda(msgStr, env);
                    continuations.Add(output.Continuation);
                    continuations.Add(Substitute(matchingInput.Continuation, matchingInput.Variable, msgStr));
                    matchedOutputs.Add(output);
                    matchedInputs.Add(matchingInput);
                }
            }

            continuations.AddRange(outputs.Except(matchedOutputs));
            continuations.AddRange(inputs.Except(matchedInputs));

            return continuations.Count switch
            {
                0 => new NullProcess(),
                1 => continuations[0],
                _ => new ParallelProcess(continuations)
            };
        }

        private Process SimulateOutputWithLambda(OutputProcess op, PiEnvironment env)
        {
            var evaluatedMessage = TryEvaluateLambda(op.Message, env);
            return new OutputProcess(op.Channel, evaluatedMessage, op.Continuation, op.IsBroadcast);
        }

        private Process Substitute(Process process, string variable, string value)
        {
            if (process is NullProcess) return process;

            if (process is OutputProcess op)
                return new OutputProcess(
                    op.Channel == variable ? value : op.Channel,
                        op.Message?.ToString() == variable ? value : op.Message,
                            Substitute(op.Continuation, variable, value),
                                op.IsBroadcast);

            if (process is InputProcess ip)
                return new InputProcess(
                    ip.Channel == variable ? value : ip.Channel,
                    ip.Variable,
                    Substitute(ip.Continuation, variable, value));

            if (process is LetProcess lp)
                return new LetProcess(lp.ResultVar, lp.Lambda, lp.ArgumentVar == variable ? value : lp.ArgumentVar,
                    Substitute(lp.Continuation, variable, value));

            return process;
        }

        private Process GetNextProcess(Process process)
        {
            return process switch
            {
                OutputProcess op => op.Continuation,
                InputProcess ip => ip.Continuation,
                LetProcess lp => lp.Continuation,
                IfElseProcess ifp => ifp.ThenBranch,
                _ => process
            };
        }

        public bool RequiresUserInput(Process currentProcess, bool isCompleted)
        {
            if (isCompleted || currentProcess is NullProcess)
                return false;

            if (currentProcess is ParallelProcess pp)
                return pp.Processes.Any(p => !(p is NullProcess));

            if (currentProcess is OutputProcess op)
                return IsLambdaExpression(op.Message);

            return !(currentProcess is NullProcess);
        }

        public VerificationResult VerifyUserStep(string userInput, string expected, Process currentProcess)
        {
            var normalizedUser = NormalizeExpression(userInput);
            var normalizedExpected = NormalizeExpression(expected);
            bool isCorrect = normalizedUser == normalizedExpected;

            return new VerificationResult
            {
                IsCorrect = isCorrect,
                UserInput = userInput,
                Expected = expected,
                Feedback = isCorrect
                    ? "✅ Правильно! Вы верно применили редукцию."
                    : $"❌ Неправильно. Ожидалось: {expected}",
                Explanation = GetStepExplanation(),
                HintForNextStep = isCorrect ? GetHintForNextStep() : "Попробуйте еще раз"
            };
        }

        public LearningStepResult CreateLearningResult(StepResult baseResult, Process currentProcess)
        {
            return new LearningStepResult
            {
                CurrentState = baseResult.CurrentState,
                LastAction = baseResult.LastAction,
                IsCompleted = baseResult.IsCompleted,
                ParallelActions = baseResult.ParallelActions,
                Variables = baseResult.Variables,
                ChannelStates = baseResult.ChannelStates,
                ActiveRestrictions = baseResult.ActiveRestrictions,

                ExpectedNextStep = _expectedNextStep,
                Hint = GetHintForCurrentStep(currentProcess),
                AvailableReductions = GetAvailableReductions(currentProcess),
                IsUserStepCorrect = null,
                Feedback = "",
                Explanation = ""
            };
        }

        private bool IsLambdaExpression(object expr)
        {
            if (expr == null) return false;
            if (expr is LambdaTerm) return true;
            if (expr is string s)
                return s.Contains("fun") || s.Contains("->");
            return false;
        }

        private object TryEvaluateLambda(object expr, PiEnvironment env)
        {
            if (expr == null) return null;
            if (expr is LambdaTerm) return expr; // оставляем как есть
            if (expr is string s && (s.Contains("fun") || s.Contains("->")))
            {
                s = SubstituteVariablesInLambda(s, env);
                try { return LambdaEvaluator.EvaluateLambda(s); }
                catch { return s; }
            }
            return expr;
        }

        private string SubstituteVariablesInLambda(string expr, PiEnvironment env)
        {
            foreach (var kv in env.Variables)
            {
                var name = kv.Key;
                var val = kv.Value?.ToString() ?? "null";
                expr = Regex.Replace(expr, $@"\b{name}\b", val);
            }
            return expr;
        }

        private string NormalizeExpression(string expr)
        {
            return expr?.Replace(" ", "").Replace("λ", "fun").ToLower() ?? "";
        }

        private string GetHintForNextStep() => "Следующий шаг: " + _expectedNextStep;

        private string GetStepExplanation()
        {
            return _reductionHistory.Count > 0
                ? _reductionHistory.Last().Explanation
                : "Выполнен шаг вычисления";
        }

        public string GetHintForCurrentStep(Process currentProcess)
        {
            return currentProcess switch
            {
                OutputProcess op when IsLambdaExpression(op.Message) =>
                    "Вычислите лямбда-выражение и введите получившееся состояние",
                ParallelProcess pp when pp.Processes.OfType<OutputProcess>().Any(op => IsLambdaExpression(op.Message)) =>
                    "Вычислите лямбда-выражения и выполните возможные коммуникации",
                ParallelProcess pp when pp.Processes.OfType<OutputProcess>().Any() && pp.Processes.OfType<InputProcess>().Any() =>
                    "Выполните коммуникации между процессами",
                _ => "Выполните шаг и введите следующее состояние процесса"
            };
        }

        public List<string> GetAvailableReductions(Process currentProcess)
        {
            var reductions = new List<string>();
            if (currentProcess is OutputProcess op && IsLambdaExpression(op.Message))
            {
                reductions.Add("beta-reduction");
                reductions.Add("alpha-conversion");
            }
            return reductions;
        }

        public string GetStepDescription(Process currentProcess)
        {
            return currentProcess switch
            {
                OutputProcess op when IsLambdaExpression(op.Message) =>
                    $"Отправка лямбда-выражения по каналу {op.Channel}",
                OutputProcess op =>
                    $"Отправка значения '{op.Message}' по каналу {op.Channel}",
                InputProcess ip =>
                    $"Ожидание получения значения в переменную {ip.Variable} из канала {ip.Channel}",
                ParallelProcess pp =>
                    $"Параллельное выполнение {pp.Processes.Count} процессов",
                _ => "Продолжите вычисление"
            };
        }
    }
}
