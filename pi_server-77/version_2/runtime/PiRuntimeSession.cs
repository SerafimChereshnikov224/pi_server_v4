using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.models; // Добавляем using для моделей


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
            return await _runtime.ExecuteStepAsync();
        }


        public async Task<LearningStepResult> ExecuteLearningStepAsync(string? userInput = null)
{
    Console.WriteLine($"=== ExecuteLearningStepAsync ===");
    Console.WriteLine($"UserInput: {userInput}");
    Console.WriteLine($"CurrentProcess: {CurrentProcess}");
    Console.WriteLine($"IsCompleted: {IsCompleted}");

    // ВРЕМЕННО: Всегда выполняем шаг, независимо от RequiresUserInput
    if (IsCompleted || CurrentProcess is NullProcess)
    {
        return new LearningStepResult
        {
            CurrentState = CurrentProcess?.ToString() ?? "0",
            IsCompleted = true,
            IsUserStepCorrect = null,
            Feedback = "🎉 Процесс завершен!",
            ExpectedNextStep = "0"
        };
    }

    // ВРЕМЕННО: Пропускаем проверку RequiresUserInput и всегда выполняем шаг
    if (_mode == LearningMode.Learning && userInput != null)
    {
        var verification = _learningService.VerifyUserStep(
            userInput,
            GetCurrentExpectedStep(),
            CurrentProcess
        );

        if (!verification.IsCorrect)
        {
            return new LearningStepResult
            {
                CurrentState = CurrentProcess?.ToString() ?? "Null state",
                IsUserStepCorrect = false,
                Feedback = verification.Feedback,
                Explanation = verification.Explanation,
                Hint = GetCurrentHint(),
                ExpectedNextStep = GetCurrentExpectedStep()
            };
        }
    }

    // ВЫПОЛНЯЕМ ШАГ В ЛЮБОМ СЛУЧАЕ
    Console.WriteLine($"Executing step...");
    var stepResult = await _runtime.ExecuteStepAsync();
    UpdateExpectedStep();

    Console.WriteLine($"Step executed. New process: {CurrentProcess}");

    var learningResult = _learningService.CreateLearningResult(stepResult, CurrentProcess);
    learningResult.IsUserStepCorrect = true;
    learningResult.Feedback = "✅ Правильно! Шаг выполнен.";

    return learningResult;
}


        public bool RequiresUserInput()
        {
            return _learningService.RequiresUserInput(CurrentProcess, IsCompleted);
        }

        public string GetCurrentHint() => _learningService.GetHintForCurrentStep(CurrentProcess);
        public string GetCurrentExpectedStep() => _learningService.GetCurrentExpectedStep();

        private void UpdateExpectedStep()
        {
            _learningService.CalculateExpectedNextStep(CurrentProcess, IsCompleted);
        }
    }
}