using PiServer.version_2.interpreter.core.syntax;

namespace PiServer.version_2.runtime
{
    public class PiRuntimeSession
    {
        private readonly PiRuntime _runtime;

        public Process CurrentProcess => _runtime.CurrentProcess;
        public bool IsCompleted => _runtime.IsCompleted;

        public PiRuntimeSession(Process process)
        {
            _runtime = new PiRuntime(process);
        }

        public async Task<StepResult> ExecuteStepAsync()
        {
            return await _runtime.ExecuteStepAsync();
        }
    }
}
