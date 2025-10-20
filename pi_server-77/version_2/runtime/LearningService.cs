// LearningService.cs
using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.models;
using PiServer.Services;

namespace PiServer.version_2.runtime
{
    public class LearningService
    {
        private readonly List<ReductionStep> _reductionHistory = new();
        private string _expectedNextStep;

        public LearningService()
        {
        }

        public string GetCurrentExpectedStep() => _expectedNextStep;

        public void CalculateExpectedNextStep(Process currentProcess, bool isCompleted)
        {
            if (isCompleted)
            {
                _expectedNextStep = "0";
                return;
            }

            // Симулируем полный шаг (вычисление лямбд + коммуникация)
            _expectedNextStep = SimulateFullStep(currentProcess).ToString();
        }

        // Главный метод симуляции полного шага
        private Process SimulateFullStep(Process process)
        {
            return process switch
            {
                ParallelProcess pp => SimulateParallelFullStep(pp),
                OutputProcess op when IsLambdaExpression(op.Message) => SimulateOutputWithLambda(op),
                _ => GetNextProcess(process) // Для других случаев просто берем следующий процесс
            };
        }

        // Симуляция полного шага для параллельных процессов
        private Process SimulateParallelFullStep(ParallelProcess pp)
        {
            // 1. Сначала вычисляем все лямбда-выражения в output процессах
            var processesAfterEval = pp.Processes.Select(p =>
            {
                if (p is OutputProcess op && IsLambdaExpression(op.Message))
                {
                    var evaluatedMessage = TryEvaluateLambda(op.Message);
                    return new OutputProcess(op.Channel, evaluatedMessage, op.Continuation);
                }
                return p;
            }).ToList();

            // 2. Симулируем коммуникацию между процессами
            return SimulateCommunication(processesAfterEval);
        }

        // Симуляция коммуникации между процессами
        private Process SimulateCommunication(List<Process> processes)
        {
            var outputs = processes.OfType<OutputProcess>().ToList();
            var inputs = processes.OfType<InputProcess>().ToList();
            var lets = processes.OfType<LetProcess>().ToList();
            
            var continuations = new List<Process>();

            // Обрабатываем let процессы
            foreach (var let in lets)
            {
                continuations.Add(let.Continuation);
            }

            // Симулируем совпадающие пары output-input
            var matchedOutputs = new List<OutputProcess>();
            var matchedInputs = new List<InputProcess>();

            foreach (var output in outputs)
            {
                var matchingInput = inputs.FirstOrDefault(input =>
                    input.Channel == output.Channel &&
                    !matchedInputs.Contains(input));

                if (matchingInput != null)
                {
                    // Коммуникация происходит - добавляем continuation процессов
                    continuations.Add(output.Continuation);
                    
                    // Для input процесса подставляем полученное значение в continuation
                    var substitutedContinuation = Substitute(matchingInput.Continuation, matchingInput.Variable, output.Message);
                    continuations.Add(substitutedContinuation);

                    matchedOutputs.Add(output);
                    matchedInputs.Add(matchingInput);
                }
            }

            // Добавляем оставшиеся процессы (которые не смогли коммуницировать)
            continuations.AddRange(outputs.Except(matchedOutputs));
            continuations.AddRange(inputs.Except(matchedInputs));

            return continuations.Count switch
            {
                0 => new NullProcess(),
                1 => continuations[0],
                _ => new ParallelProcess(continuations)
            };
        }

        // Симуляция для output процесса с лямбдой
        private Process SimulateOutputWithLambda(OutputProcess op)
        {
            var evaluatedMessage = TryEvaluateLambda(op.Message);
            return new OutputProcess(op.Channel, evaluatedMessage, op.Continuation);
        }

        // Вспомогательный метод для подстановки (аналогичный тому, что в PiRuntime)
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

        // Получение следующего процесса (для не-параллельных случаев)
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


        public bool RequiresUserInput(Process currentProcess, bool isCompleted)
{
    if (isCompleted || currentProcess is NullProcess)
        return false;

    // Для параллельных процессов проверяем есть ли активные процессы (не 0)
    if (currentProcess is ParallelProcess pp)
    {
        // Если есть процессы которые не являются NullProcess (0)
        return pp.Processes.Any(p => !(p is NullProcess));
    }

    // Для одиночных output процессов с лямбда-выражениями
    if (currentProcess is OutputProcess op)
    {
        return IsLambdaExpression(op.Message);
    }

    // Если это не NullProcess, то требуется ввод
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
                Feedback = isCorrect ? 
                    "✅ Правильно! Вы верно применили редукцию." : 
                    $"❌ Неправильно. Ожидалось: {expected}",
                Explanation = GetStepExplanation(),
                HintForNextStep = isCorrect ? GetHintForNextStep() : "Попробуйте еще раз"
            };
        }

        public LearningStepResult CreateLearningResult(StepResult baseResult, Process currentProcess)
        {
            return new LearningStepResult
            {
                // Базовые свойства
                CurrentState = baseResult.CurrentState,
                LastAction = baseResult.LastAction,
                IsCompleted = baseResult.IsCompleted,
                ParallelActions = baseResult.ParallelActions,
                Variables = baseResult.Variables,
                ChannelStates = baseResult.ChannelStates,
                ActiveRestrictions = baseResult.ActiveRestrictions,
                
                // Дополнительные свойства для обучения
                ExpectedNextStep = _expectedNextStep,
                Hint = GetHintForCurrentStep(currentProcess),
                AvailableReductions = GetAvailableReductions(currentProcess),
                IsUserStepCorrect = null,
                Feedback = "",
                Explanation = ""
            };
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

        // Вспомогательные методы (остаются private)
        private bool IsLambdaExpression(string expression)
        {
            return !string.IsNullOrEmpty(expression) &&
                   (expression.Contains("fun") || expression.Contains("->"));
        }

        private string TryEvaluateLambda(string expression)
        {
            try
            {
                return LambdaEvaluator.EvaluateLambda(expression);
            }
            catch
            {
                return expression;
            }
        }

        private string NormalizeExpression(string expr)
        {
            return expr?.Replace(" ", "").Replace("λ", "fun").ToLower() ?? "";
        }

        private string GetHintForNextStep() => "Следующий шаг: " + _expectedNextStep;

        private string GetStepExplanation()
        {
            return _reductionHistory.Count > 0 ? 
                _reductionHistory.Last().Explanation : "Выполнен шаг вычисления";
        }
    }
}