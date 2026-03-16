using PiServer.version_2.interpreter.core.syntax;
using System.Collections.Generic;
using PiServer.version_2.models;

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
    }
}