using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.models;
using System;
using System.Threading.Tasks;

namespace PiServer.version_2.runtime
{
    public class PiRuntimeSession
    {
        private readonly PiRuntime _runtime;
        private readonly LearningService _learningService;
        private LearningMode _mode = LearningMode.Auto;

        public Process CurrentProcess => _runtime.CurrentProcess;
        public bool IsCompleted => _runtime.IsCompleted;
        public LearningMode Mode => _mode;
        public string GetStepDescription() => _learningService.GetStepDescription(CurrentProcess);

        public PiRuntimeSession(Process process, LearningMode mode = LearningMode.Auto)
        {
            _runtime = new PiRuntime(process);
            _learningService = new LearningService();
            _mode = mode;
            UpdateExpectedStep();

            Console.WriteLine($"=== PiRuntimeSession Created ===");
            Console.WriteLine($"Mode: {mode}");
            Console.WriteLine($"Initial Process: {process}");
        }

        public async Task<StepResult> ExecuteStepAsync()
        {
            var stepResult = await _runtime.ExecuteStepAsync();
            UpdateExpectedStep();

            // Подставляем значения переменных в текущее состояние
            stepResult.CurrentState = _runtime.SubstituteVariablesInExpression(stepResult.CurrentState, _runtime._env);

            return stepResult;
        }

        public async Task<LearningStepResult> ExecuteLearningStepAsync(string? userInput = null)
        {
            Console.WriteLine($"=== ExecuteLearningStepAsync ===");
            Console.WriteLine($"UserInput: {userInput}");
            Console.WriteLine($"CurrentProcess: {CurrentProcess}");
            Console.WriteLine($"IsCompleted: {IsCompleted}");

            if (IsCompleted || CurrentProcess is NullProcess)
            {
                return new LearningStepResult
                {
                    CurrentState = _runtime.SubstituteVariablesInExpression(CurrentProcess?.ToString() ?? "0", _runtime._env),
                    IsCompleted = true,
                    IsUserStepCorrect = null,
                    Feedback = "🎉 Процесс завершен!",
                    ExpectedNextStep = "0"
                };
            }

            if (_mode == LearningMode.Learning && userInput != null)
            {
                var normalizedUserInput = _runtime.SubstituteVariablesInExpression(userInput, _runtime._env);

                var verification = _learningService.VerifyUserStep(
                    normalizedUserInput,
                    _runtime.SubstituteVariablesInExpression(GetCurrentExpectedStep(), _runtime._env),
                    CurrentProcess
                );

                if (!verification.IsCorrect)
                {
                    return new LearningStepResult
                    {
                        CurrentState = _runtime.SubstituteVariablesInExpression(CurrentProcess?.ToString() ?? "Null state", _runtime._env),
                        IsUserStepCorrect = false,
                        Feedback = verification.Feedback,
                        Explanation = verification.Explanation,
                        Hint = GetCurrentHint(),
                        ExpectedNextStep = _runtime.SubstituteVariablesInExpression(GetCurrentExpectedStep(), _runtime._env)
                    };
                }
            }

            // Выполняем шаг
            var stepResult = await _runtime.ExecuteStepAsync();
            UpdateExpectedStep();

            var learningResult = _learningService.CreateLearningResult(stepResult, CurrentProcess);
            learningResult.IsUserStepCorrect = true;
            learningResult.Feedback = "✅ Правильно! Шаг выполнен.";

            // Подставляем значения переменных в результат
            learningResult.CurrentState = _runtime.SubstituteVariablesInExpression(learningResult.CurrentState, _runtime._env);
            learningResult.ExpectedNextStep = _runtime.SubstituteVariablesInExpression(learningResult.ExpectedNextStep, _runtime._env);

            return learningResult;
        }

        public bool RequiresUserInput() => _learningService.RequiresUserInput(CurrentProcess, IsCompleted);

        public string GetCurrentHint() => _learningService.GetHintForCurrentStep(CurrentProcess);
        public string GetCurrentExpectedStep() => _learningService.GetCurrentExpectedStep();

        private void UpdateExpectedStep()
        {
            _learningService.CalculateExpectedNextStep(CurrentProcess, _runtime._env, IsCompleted);

            // Подставляем переменные сразу после расчета
            var expected = _learningService.GetCurrentExpectedStep();
            expected = _runtime.SubstituteVariablesInExpression(expected, _runtime._env);
        }

    }
}
