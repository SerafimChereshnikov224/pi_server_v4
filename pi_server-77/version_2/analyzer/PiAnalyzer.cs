using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.models;
using System.Collections.Generic;
using System.Threading.Tasks;
using PiServer.version_2.runtime;

namespace PiServer.version_2.analyzer
{
    public static class PiAnalyzer
    {
        public static AnalysisResult Analyze(Process process)
        {
            var context = new AnalysisContext();
            VisitProcess(process, context);
            return context.BuildResult();
        }

        private class AnalysisContext
        {
            public HashSet<string> AllNames = new();
            public HashSet<string> Defined = new();
            public HashSet<string> UsedAsInput = new();
            public HashSet<string> UsedAsOutput = new();
            public List<string> DeadProcesses = new();

            public AnalysisResult BuildResult()
            {
                var used = new HashSet<string>(UsedAsInput);
                used.UnionWith(UsedAsOutput);

                return new AnalysisResult
                {
                    AllChannelNames = AllNames,
                    DefinedChannels = Defined,
                    UsedChannels = used,
                    InputOnlyChannels = new HashSet<string>(UsedAsInput.Except(UsedAsOutput)),
                    OutputOnlyChannels = new HashSet<string>(UsedAsOutput.Except(UsedAsInput)),
                    DeadProcessDescriptions = DeadProcesses
                };
            }
        }

        private static void VisitProcess(Process process, AnalysisContext context)
        {
            switch (process)
            {
                case NullProcess:
                    break;

                case OutputProcess op:
                    context.AllNames.Add(op.Channel);
                    context.UsedAsOutput.Add(op.Channel);

                    VisitProcess(op.Continuation, context);
                    break;

                case InputProcess ip:
                    context.AllNames.Add(ip.Channel);
                    context.UsedAsInput.Add(ip.Channel);

                    VisitProcess(ip.Continuation, context);
                    break;

                case ParallelProcess pp:
                    foreach (var p in pp.Processes)
                        VisitProcess(p, context);

                    foreach (var p in pp.Processes)
                    {
                        if (p is NullProcess)
                            context.DeadProcesses.Add("Вероятно бесполезный нуль-процесс обнаружен в параллельной композиции");
                    }
                    break;

                case RestrictionProcess rp:
                    context.Defined.Add(rp.Name);
                    context.AllNames.Add(rp.Name);
                    VisitProcess(rp.Body, context);
                    break;

                case IfElseProcess ifp:

                    VisitProcess(ifp.ThenBranch, context);
                    VisitProcess(ifp.ElseBranch, context);
                    break;

                case LetProcess lp:

                    VisitProcess(lp.Continuation, context);
                    break;

                default:
                    break;
            }
        }

        public static async Task<SimulationResult> SimulateAsync(Process process, int maxSteps = 1000)
        {
            var runtime = new PiRuntime(process);
            var result = new SimulationResult();

            for (int step = 0; step < maxSteps; step++)
            {
                if (runtime.IsCompleted)
                {
                    result.IsDeadlocked = false;
                    result.StepsExecuted = step;
                    result.FinalState = runtime.CurrentProcess.ToString();
                    return result;
                }

                if (runtime.IsDeadlocked())
                {
                    result.IsDeadlocked = true;
                    result.StepsExecuted = step;
                    result.FinalState = runtime.CurrentProcess.ToString();

                    var inputs = runtime.CollectInputs(runtime.CurrentProcess);
                    var outputs = runtime.CollectOutputs(runtime.CurrentProcess);
                    var allBlocked = new List<string>();
                    allBlocked.AddRange(inputs.Select(ip => ip.ToString()));
                    allBlocked.AddRange(outputs.Select(op => op.ToString()));
                    result.DeadlockedProcesses = allBlocked;
                    return result;
                }

                await runtime.ExecuteStepAsync();
            }

            // Лимит шагов достигнут
            result.IsDeadlocked = runtime.IsDeadlocked();
            result.StepsExecuted = maxSteps;
            result.FinalState = runtime.CurrentProcess.ToString();
            if (result.IsDeadlocked)
            {
                var inputs = runtime.CollectInputs(runtime.CurrentProcess);
                var outputs = runtime.CollectOutputs(runtime.CurrentProcess);
                result.DeadlockedProcesses = inputs.Select(ip => ip.ToString())
                    .Concat(outputs.Select(op => op.ToString()))
                    .ToList();
            }
            return result;
        }
    }
}