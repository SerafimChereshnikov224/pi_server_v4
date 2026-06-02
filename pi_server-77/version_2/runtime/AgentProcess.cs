using PiServer.version_2.interpreter.core;
using PiServer.version_2.interpreter.core.syntax;

namespace PiServer.version_2.runtime
{
    /// Агент — именованный контейнер процесса.
    /// Не изменяет семантику π-исчисления.
    public class AgentProcess : Process
    {
        public string AgentName { get; }

        public Process InnerProcess { get; }

        public AgentProcess(string agentName, Process innerProcess)
        {
            AgentName = agentName;
            InnerProcess = innerProcess;
        }

        public override async Task ExecuteAsync(PiEnvironment env)
        {
            await InnerProcess.ExecuteAsync(env);
        }

        public override string ToString()
        {
            return $"Agent({AgentName}) {{ {InnerProcess} }}";
        }
    }
}