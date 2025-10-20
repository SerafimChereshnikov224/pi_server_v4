// PiServer.version_2.models/ProcessModels.cs
namespace PiServer.version_2.models
{
    public class ProcessRequest
    {
        public string ProcessDefinition { get; set; }
    }

    public class ProcessResponse
    {
        public string SessionId { get; set; }
        public string CurrentState { get; set; }
    }

    public class ProcessState
    {
        public string CurrentState { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class LambdaRequest
    {
        public string Expression { get; set; }
    }

    // StepResult должен быть здесь, так как он используется в базовой логике
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