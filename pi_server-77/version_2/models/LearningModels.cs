// PiServer.version_2.models/LearningModels.cs
namespace PiServer.version_2.models
{
    public class LearningRequest : ProcessRequest
    {
        public string Mode { get; set; } = "learning";
    }

    public class StepVerificationRequest
    {
        public string UserInput { get; set; }
    }

    // LearningStepResult наследуется от StepResult
    public class LearningStepResult : StepResult
    {
        public string ExpectedNextStep { get; set; }
        public bool? IsUserStepCorrect { get; set; }
        public string Feedback { get; set; }
        public string Hint { get; set; }
        public List<string> AvailableReductions { get; set; } = new();
        public string Explanation { get; set; }
    }

    public class VerificationResult
    {
        public bool IsCorrect { get; set; }
        public string UserInput { get; set; }
        public string Expected { get; set; }
        public string Feedback { get; set; }
        public string Explanation { get; set; }
        public string HintForNextStep { get; set; }
    }

    public enum LearningMode
    {
        Auto,
        Learning
    }

    public class ReductionStep
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Rule { get; set; }
        public string Explanation { get; set; }
    }
}