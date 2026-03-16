using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.runtime;
using PiServer.version_2.analyzer;
using Xunit;
using System.Diagnostics; 


namespace PiServer.version_2.interpreter.tests
{
    public class PiParserTests
    {

        public class ParserTests
        {

            [Fact]
            public async Task ExecuteStep_WikipediaExampleNoRestriction_LogsAndVerifies()
            {
                // Процесс: x![z].0 | x?(y). y![x]. x?(y).0 | z?(v). v![v].0
                var expression = "x![z].0 | x?(y). y![x]. x?(y).0 | z?(v). v![v].0";
                var parser = new PiParser(expression);
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var stepResults = new List<StepResult>();
                var log = new System.Text.StringBuilder();

                log.AppendLine("=== Start ===");
                log.AppendLine($"Initial: {session.CurrentProcess}");

                // Шаг 1: коммуникация по x между x![z] и x?(y)
                var step1 = await session.ExecuteStepAsync();
                stepResults.Add(step1);
                log.AppendLine($"Step 1: {step1.LastAction}");
                log.AppendLine($"State: {step1.CurrentState}");
                log.AppendLine($"Variables: {string.Join(", ", step1.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
                log.AppendLine($"Channels: {string.Join(", ", step1.ChannelStates.Select(c => $"{c.Key}=[{string.Join(",", c.Value)}]"))}");
                // Ожидаемое после шага 1: z![x].0 | x?(y).0 | z?(v). v![v].0   (на самом деле после первого шага B становится z![x]. x?(y).0, A исчезает, C не тронут)
                //Проверим наличие z![x].0 и z?(v).v![v].0 и x?(y).0(второе вхождение)
                Assert.Contains("z?(v).v![v].0", step1.CurrentState);
                Assert.False(step1.IsCompleted);

                // Шаг 2: коммуникация по z между z![x] и z?(v)
                var step2 = await session.ExecuteStepAsync();
                stepResults.Add(step2);
                log.AppendLine($"Step 2: {step2.LastAction}");
                log.AppendLine($"State: {step2.CurrentState}");
                log.AppendLine($"Variables: {string.Join(", ", step2.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
                log.AppendLine($"Channels: {string.Join(", ", step2.ChannelStates.Select(c => $"{c.Key}=[{string.Join(",", c.Value)}]"))}");
                // Ожидаемое после шага 2: x?(y).0 | x![x].0
                Assert.Contains("x?(y).0", step2.CurrentState);
                Assert.Contains("x![x].0", step2.CurrentState);
                Assert.DoesNotContain("z![x].0", step2.CurrentState);
                Assert.DoesNotContain("z?(v). v![v].0", step2.CurrentState);
                Assert.False(step2.IsCompleted);

                // Шаг 3: коммуникация по x между x![x] и x?(y)
                var step3 = await session.ExecuteStepAsync();
                stepResults.Add(step3);
                log.AppendLine($"Step 3: {step3.LastAction}");
                log.AppendLine($"State: {step3.CurrentState}");
                log.AppendLine($"Variables: {string.Join(", ", step3.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
                log.AppendLine($"Channels: {string.Join(", ", step3.ChannelStates.Select(c => $"{c.Key}=[{string.Join(",", c.Value)}]"))}");
                // Ожидаемое после шага 3: 0
                Assert.True(step3.IsCompleted);
                Assert.Equal("0", step3.CurrentState);

                log.AppendLine("=== End ===");

                System.Console.WriteLine(log.ToString());               
            }


            [Fact]
            public void ParseNullProcess()
            {
                var parser = new PiParser("0");
                var result = parser.Parse();

                Assert.IsType<NullProcess>(result);
                Assert.Equal("0", result.ToString());
            }

            [Fact]
            public void ParseOutputProcess()
            {
                var parser = new PiParser("x![y].0");
                var result = parser.Parse();

                var output = Assert.IsType<OutputProcess>(result);
                Assert.Equal("x", output.Channel);
                Assert.Equal("y", output.Message);
                Assert.IsType<NullProcess>(output.Continuation);
            }

            [Fact]
            public void ParseInputProcess()
            {
                var parser = new PiParser("a?(b).0");
                var result = parser.Parse();

                var input = Assert.IsType<InputProcess>(result);
                Assert.Equal("a", input.Channel);
                Assert.Equal("b", input.Variable); 
                Assert.IsType<NullProcess>(input.Continuation);
            }

            [Fact]
            public void ParseParallelProcess()
            {
                var parser = new PiParser("a![b].0 | c?(d).0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);

                Assert.Equal(2, parallel.Processes.Count);

                var firstProcess = parallel.Processes[0] as OutputProcess;
                var secondProcess = parallel.Processes[1] as InputProcess;

                Assert.NotNull(firstProcess);
                Assert.NotNull(secondProcess);
                Assert.Equal("a", firstProcess.Channel);
                Assert.Equal("c", secondProcess.Channel);
            }

            [Fact]
            public void ParseNestedProcess()
            {
                var parser = new PiParser("a?(x).(x![done].0 | b?(y).0)");
                var result = parser.Parse();

                var input = Assert.IsType<InputProcess>(result);
                Assert.Equal("a", input.Channel);
                Assert.Equal("x", input.Variable);

                var nestedParallel = Assert.IsType<ParallelProcess>(input.Continuation);
                Assert.Equal(2, nestedParallel.Processes.Count);

                var leftOutput = nestedParallel.Processes[0] as OutputProcess;
                var rightInput = nestedParallel.Processes[1] as InputProcess;

                Assert.NotNull(leftOutput);
                Assert.NotNull(rightInput);
                Assert.Equal("x", leftOutput.Channel);
                Assert.Equal("b", rightInput.Channel);
            }

            [Fact]
            public void ParseRestrictionProcess()
            {
                var parser = new PiParser("{*x}x![done].0");
                var result = parser.Parse();

                var restriction = Assert.IsType<RestrictionProcess>(result);
                Assert.Equal("x", restriction.Name);
                Assert.IsType<OutputProcess>(restriction.Body);
            }

            [Fact]
            public void ParseComplexNestedProcess()
            {
                var parser = new PiParser("a![x].(done![x].0 | a?(y).(y![done].0 | d![end].0))");
                var result = parser.Parse();

                var input = Assert.IsType<OutputProcess>(result);
                var nestedParallel = Assert.IsType<ParallelProcess>(input.Continuation);

                Assert.Equal(2, nestedParallel.Processes.Count);

                var outputProcess = nestedParallel.Processes[0] as OutputProcess;
                var nestedInput = nestedParallel.Processes[1] as InputProcess;

                Assert.NotNull(outputProcess);
                Assert.NotNull(nestedInput);
                Assert.Equal("done", outputProcess.Channel);
                Assert.Equal("a", nestedInput.Channel);
            }

            [Fact]
            public void ParseMultipleParallelProcesses()
            {
                var parser = new PiParser("a![null].0 | b![null].0 | c![null].0 | d![null].0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);
                Assert.Equal(4, parallel.Processes.Count);

                foreach (var process in parallel.Processes)
                {
                    Assert.IsType<OutputProcess>(process);
                }
            }

            [Fact]
            public void ParseDeeplyNestedProcess()
            {
                var parser = new PiParser("a?(x).(b?(y).(c?(z).(z![null].0 | d![null].0) | e![null].0) | f![null].0)");
                var result = parser.Parse();

                Assert.NotNull(result);
                Assert.IsType<InputProcess>(result);
            }

            [Fact]
            public void ParseBroadcastOutputProcess()
            {
                var parser = new PiParser("a!![msg].0");
                var result = parser.Parse();

                var output = Assert.IsType<OutputProcess>(result);
                Assert.Equal("a", output.Channel);
                Assert.Equal("msg", output.Message);
                Assert.IsType<NullProcess>(output.Continuation);
                Assert.True(output.IsBroadcast);
            }

            [Fact]
            public void ParseNormalOutputProcess_IsNotBroadcast()
            {
                var parser = new PiParser("a![msg].0");
                var result = parser.Parse();

                var output = Assert.IsType<OutputProcess>(result);
                Assert.False(output.IsBroadcast);
            }

        }

        public class StepExecutionTests
        {
            [Fact]
            public async Task ExecuteStep_SimpleCommunication_Completes()
            {
                var parser = new PiParser("a![b].0 | a?(x).0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result1 = await session.ExecuteStepAsync();

                Assert.True(result1.IsCompleted);
                Assert.DoesNotContain("a![b].0", result1.CurrentState);
                Assert.DoesNotContain("a?(x).0", result1.CurrentState);
                Assert.Contains("0", result1.CurrentState);

            }

            [Fact]
            public async Task ExecuteStep_ChannelPassing()
            {
                var parser = new PiParser("a![x].0 | a?(c).y![done].0 | y?(smth).smth![done].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result1 = await session.ExecuteStepAsync();
                Assert.False(result1.IsCompleted);
                Assert.Contains("y![done].0", result1.CurrentState);
                Assert.Contains("y?(smth).smth![done].0", result1.CurrentState);

                var result2 = await session.ExecuteStepAsync();
                Assert.False(result2.IsCompleted);
                Assert.Contains("done![done].0", result2.CurrentState);

                var result3 = await session.ExecuteStepAsync();
                Assert.True(result3.IsCompleted);
            }

            [Fact]
            public async Task ExecuteStep_DeadlockDetection()
            {
                var parser = new PiParser("a![x].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                Assert.True(result.IsCompleted);
            }
            [Fact]
            public async Task ExecuteStep_MultipleParallelCommunications()
            {
                var parser = new PiParser("a![msg1].0 | b![msg2].0 | a?(x).0 | b?(y).0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result1 = await session.ExecuteStepAsync();
                Assert.True(result1.IsCompleted);

            }

            // [Fact]
            // public async Task ExecuteStep_NestedCommunicationChain()
            // {
            //     var parser = new PiParser("a?(x).x![response].0 | a![b].0 | b?(y).y![final].0");
            //     var process = parser.Parse();
            //     var session = new PiRuntimeSession(process);

            //     var result1 = await session.ExecuteStepAsync();
            //     Assert.False(result1.IsCompleted);
            //     Assert.Contains("b![response].0", result1.CurrentState);

            //     var result2 = await session.ExecuteStepAsync();
            //     Assert.False(result2.IsCompleted);
            //     Assert.Contains("response![final].0", result2.CurrentState);

            //     var result3 = await session.ExecuteStepAsync();
            //     Assert.False(result3.IsCompleted);

            //     var result4 = await session.ExecuteStepAsync();
            //     Assert.True(result4.IsCompleted);
            // }

            // [Fact]
            // public async Task ExecuteStep_RestrictionWithCommunication()
            // {
            //     // Arrange
            //     var parser = new PiParser("{*x}(x![secret].0 | x?(y).0)");
            //     var process = parser.Parse();
            //     var session = new PiRuntimeSession(process);

            //     // Act - Step 1 (коммуникация внутри restriction)
            //     var result = await session.ExecuteStepAsync();

            //     // Assert
            //     Assert.True(result.IsCompleted);
            // }

            [Fact]
            public async Task Broadcast_DeliversToAllWaitingReceivers()
            {
                // Процесс: broadcast по каналу a и два получателя
                var parser = new PiParser("a!![hello].0 | a?(x).out1![x].0 | a?(y).out2![y].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                // После одного шага broadcast должен доставить сообщение обоим,
                // значит должны появиться out1![hello] и out2![hello]
                Assert.False(result.IsCompleted);
                Assert.Contains("out1![hello].0", result.CurrentState);
                Assert.Contains("out2![hello].0", result.CurrentState);
                Assert.DoesNotContain("a!![hello].0", result.CurrentState);
                Assert.DoesNotContain("a?(x)", result.CurrentState);
                Assert.DoesNotContain("a?(y)", result.CurrentState);
            }

            [Fact]
            public async Task Broadcast_WithNoReceivers_LosesMessage()
            {
                System.Console.WriteLine("=== BROADCAST TEST RUNNING ===");
                // Отправка broadcast без получателей: сообщение не сохраняется, процесс завершается
                var parser = new PiParser("a!![hello].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                Assert.True(result.IsCompleted);
                Assert.Equal("0", result.CurrentState);
            }

            [Fact]
            public async Task Broadcast_DoesNotAffectUnicastSemantics()
            {
                var parser = new PiParser("a!![broadcast].0 | a![unicast].0 | a?(x).p1![x].0 | a?(y).p2![y].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                // Диагностика
                Console.WriteLine($"CurrentState: {result.CurrentState}");
                Console.WriteLine($"IsCompleted: {result.IsCompleted}");
                Console.WriteLine($"Variables: {string.Join(", ", result.Variables)}");
                Console.WriteLine($"Contains p1![broadcast]: {result.CurrentState.Contains("p1![broadcast].0")}");
                Console.WriteLine($"Contains p2![broadcast]: {result.CurrentState.Contains("p2![broadcast].0")}");
                Console.WriteLine($"Contains p1![unicast]: {result.CurrentState.Contains("p1![unicast].0")}");
                Console.WriteLine($"Contains p2![unicast]: {result.CurrentState.Contains("p2![unicast].0")}");
                Console.WriteLine($"Contains a![unicast]: {result.CurrentState.Contains("a![unicast].0")}");

                Assert.Contains("p1![broadcast].0", result.CurrentState);
                Assert.Contains("p2![broadcast].0", result.CurrentState);

                bool unicastExecuted = result.CurrentState.Contains("p1![unicast].0") ||
                                       result.CurrentState.Contains("p2![unicast].0");
                bool unicastRemained = result.CurrentState.Contains("a![unicast].0");
                bool processCompleted = result.IsCompleted && result.CurrentState == "0";
                Assert.True(unicastExecuted || unicastRemained || processCompleted,
                    "Unicast должен либо выполниться, либо остаться в виде невыполненного вывода, либо процесс завершиться");
            }

            [Fact]
            public async Task Broadcast_WithMultipleBroadcasts_HandlesCorrectly()
            {
                // Два broadcast подряд по разным каналам
                var parser = new PiParser("a!![msg1].0 | b!![msg2].0 | a?(x).p![x].0 | b?(y).q![y].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                Assert.Contains("p![msg1].0", result.CurrentState);
                Assert.Contains("q![msg2].0", result.CurrentState);
            }

            [Fact]
            public async Task Broadcast_WithLambdaMessage_EvaluatesCorrectly()
            {
                // Broadcast с лямбда-выражением в сообщении
                var parser = new PiParser("a!![(fun x -> x + 1) 5].0 | a?(y).out![y].0 | a?(z).out2![z].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                Assert.Contains("out![6].0", result.CurrentState);
                Assert.Contains("out2![6].0", result.CurrentState);
            }

            [Fact]
            public async Task Broadcast_WithComplexContinuations_ExecutesAllBranches()
            {
                // Получатели имеют сложные продолжения (if, parallel)
                var parser = new PiParser("a!![10].0 | a?(x).if x > 5 then b![x].0 else 0 | a?(y).(c![y].0 | d![y].0)");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                // После broadcast оба получателя активируются:
                // первый: if (10 > 5) -> b![10].0
                // второй: параллельный процесс c![10].0 | d![10].0
                Assert.Contains("b![10].0", result.CurrentState);
                Assert.Contains("c![10].0", result.CurrentState);
                Assert.Contains("d![10].0", result.CurrentState);
            }
        }

        public class LambdaParserTests
        {
            [Fact]
            public void ParseLetProcess_WithIdentityLambda()
            {
                var parser = new PiParser("let z = (λx.x) y.0");
                var result = parser.Parse();

                var letProcess = Assert.IsType<LetProcess>(result);
                Assert.Equal("z", letProcess.ResultVar);
                Assert.Equal("y", letProcess.ArgumentVar);
                Assert.IsType<NullProcess>(letProcess.Continuation);

                Assert.NotNull(letProcess.Lambda);
            }

            // [Fact]
            // public void ParseLetProcess_WithComplexLambda()
            // {
            //     var parser = new PiParser("let result = (λx.λy.x + y) a b.0");
            //     var result = parser.Parse();

            //     var letProcess = Assert.IsType<LetProcess>(result);
            //     Assert.Equal("result", letProcess.ResultVar);
            //     Assert.Equal("b", letProcess.ArgumentVar); // Последний аргумент
            //     Assert.IsType<NullProcess>(letProcess.Continuation);
            // }

            // [Fact]
            // public void ParseLetProcess_WithMultipleArguments()
            // {
            //     var parser = new PiParser("let z = (fun x y -> x * y) 5 3.0");
            //     var result = parser.Parse();

            //     var letProcess = Assert.IsType<LetProcess>(result);
            //     Assert.Equal("z", letProcess.ResultVar);
            //     Assert.IsType<NullProcess>(letProcess.Continuation);
            // }

            [Fact]
            public async Task ExecuteStep_LambdaInOutput_Evaluates()
            {
                var parser = new PiParser("a![(fun x -> x * 2) 5].0 | a?(y).out![y].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result1 = await session.ExecuteStepAsync();
                var result2 = await session.ExecuteStepAsync();

                Assert.True(result2.IsCompleted);
                Assert.Contains("0", result2.CurrentState); // 5 * 2 = 10
                Assert.DoesNotContain("fun x -> x * 2", result2.CurrentState);
            }

            [Fact]
            public async Task ExecuteStep_SimpleLambdaInOutput_Evaluates()
            {
                var parser = new PiParser("a![(fun x->x)hello].0 | a?(y).out![y].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result1 = await session.ExecuteStepAsync();
                var result2 = await session.ExecuteStepAsync();

                Assert.True(result2.IsCompleted);
                Assert.Contains("0", result2.CurrentState);
                Assert.DoesNotContain("λx.x", result2.CurrentState);
            }


            [Fact]
            public void Parse_ParallelProcess_WithNestedLambdaCommunication()
            {
                var parser = new PiParser("n![4].0 | n?(a).y![(fun x -> (fun y -> x + y) a) 10].0 | y?(res).0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);
                Assert.Equal(3, parallel.Processes.Count);

                Assert.IsType<OutputProcess>(parallel.Processes[0]);
                Assert.IsType<InputProcess>(parallel.Processes[1]);
                Assert.IsType<InputProcess>(parallel.Processes[2]);
            }

            [Fact]
            public void Parse_ParallelProcess_WithLambdaResultSend()
            {
                var parser = new PiParser("a![(fun x -> x * 2) 5].0 | a?(r).b![r].0 | b?(res).0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);
                Assert.Equal(3, parallel.Processes.Count);

                var send = Assert.IsType<OutputProcess>(parallel.Processes[0]);
                Assert.Contains("fun x -> x * 2", send.Message.ToString());
            }
            
            [Fact]
            public void Parse_ParallelProcess_WithLambdaDependingOnInput()
            {
                var parser = new PiParser("x![8].0 | x?(n).if n > 5 then y![(fun x -> x + n) 3].0 else y![0].0 | y?(res).0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);
                Assert.Equal(3, parallel.Processes.Count);

                Assert.IsType<OutputProcess>(parallel.Processes[0]);
                Assert.IsType<InputProcess>(parallel.Processes[1]);
            }


            [Fact]
            public void Parse_ParallelProcess_WithSequentialLambdaEvaluations()
            {
                var parser = new PiParser("a![(fun x -> x + 1) 2].0 | a?(v).b![(fun y -> y * v) 3].0 | b?(r).0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);
                Assert.Equal(3, parallel.Processes.Count);

                var output1 = Assert.IsType<OutputProcess>(parallel.Processes[0]);
                var input2 = Assert.IsType<InputProcess>(parallel.Processes[1]);
                var output2 = Assert.IsType<OutputProcess>(input2.Continuation);
            }

            [Fact]
            public void Parse_LambdaInsideNestedChannels()
            {
                var parser = new PiParser("x![(fun z -> (fun t -> z + t)2) 1].0 | x?(f).f![2].0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);
                Assert.Equal(2, parallel.Processes.Count);

                var output = Assert.IsType<OutputProcess>(parallel.Processes[0]);
                Assert.Contains("fun z ->", output.Message.ToString());
            }

            [Fact]
            public void Parse_MultiStepCommunicationWithIfAndLambda()
            {
                var parser = new PiParser("a![10].0 | a?(n).if n > 5 then b![(fun x -> n + x) 2].0 else b![0].0 | b?(res).0");
                var result = parser.Parse();

                var parallel = Assert.IsType<ParallelProcess>(result);
                Assert.Equal(3, parallel.Processes.Count);

                var input = Assert.IsType<InputProcess>(parallel.Processes[1]);
                var ifProc = Assert.IsType<IfElseProcess>(input.Continuation);
                Assert.IsType<OutputProcess>(ifProc.ThenBranch);
            }

            
    
    
        }


            

        public class EdgeCaseTests
        {
            [Fact]
            public async Task ExecuteStep_EmptyParallelProcess()
            {
                var parser = new PiParser("0 | 0 | 0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                Assert.True(session.IsCompleted);
            }

            [Fact]
            public async Task ExecuteStep_OutputWithoutInput_Completes()
            {
                var parser = new PiParser("a![message].0 | b![another].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                Assert.True(result.IsCompleted);
               
            }

            [Fact]
            public async Task ExecuteStep_InputWithoutOutput_Deadlocks()
            {
                var parser = new PiParser("a?(x).0 | b?(y).0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                Assert.False(result.IsCompleted);
            }
        }

        public class PerformanceTests
        {
            [Fact]
            public async Task ExecuteStep_LargeParallelProcess_Completes()
            {
                var processes = new List<string>();
                for (int i = 0; i < 10; i++)
                {
                    processes.Add($"channel{i}![data{i}].0");
                    processes.Add($"channel{i}?(x).0");
                }

                var processDefinition = string.Join(" | ", processes);
                var parser = new PiParser(processDefinition);
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                StepResult result = null;
                int stepCount = 0;

                do
                {
                    result = await session.ExecuteStepAsync();
                    stepCount++;
                } while (!result.IsCompleted && stepCount < 100); 

                Assert.True(result.IsCompleted);
                Assert.True(stepCount <= 10); 
            }

            [Fact]
            public void Parse_ComplexExpression_PerformsWell()
            {
                var complexExpression = "a?(x).(b?(y).(c?(z).(d![x].0 | e![y].0 | f![z].0) | g![null].0) | h![null].0) | i![null].0";

                var parser = new PiParser(complexExpression);
                var stopwatch = Stopwatch.StartNew();

                var result = parser.Parse();

                stopwatch.Stop();
                Assert.NotNull(result);
                Assert.True(stopwatch.ElapsedMilliseconds < 1000, "Parsing took too long");
            }
        
        }

        public class AnalyzerTests
        {
            [Fact]
            public void Analyze_SimpleOutput_DetectsUsedChannel()
            {
                var parser = new PiParser("x![y].0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("x", result.UsedChannels);
                Assert.Contains("x", result.OutputOnlyChannels);
                Assert.DoesNotContain("x", result.InputOnlyChannels);
                Assert.Empty(result.DefinedChannels);
                Assert.Empty(result.UnusedDefinedChannels);
            }

            [Fact]
            public void Analyze_SimpleInput_DetectsUsedChannel()
            {
                var parser = new PiParser("a?(b).0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("a", result.UsedChannels);
                Assert.Contains("a", result.InputOnlyChannels);
                Assert.DoesNotContain("a", result.OutputOnlyChannels);
            }

            [Fact]
            public void Analyze_Restriction_AddsDefinedChannel()
            {
                var parser = new PiParser("{*x}x![y].0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("x", result.DefinedChannels);
                Assert.Contains("x", result.UsedChannels); // используется в output
                Assert.Contains("x", result.OutputOnlyChannels);
                Assert.Empty(result.UnusedDefinedChannels);
            }

            [Fact]
            public void Analyze_UnusedRestrictedChannel_ReportsUnused()
            {
                var parser = new PiParser("{*x}0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("x", result.DefinedChannels);
                Assert.DoesNotContain("x", result.UsedChannels);
                Assert.Contains("x", result.UnusedDefinedChannels);
            }

            [Fact]
            public void Analyze_ParallelWithNull_ReportsDeadProcess()
            {
                var parser = new PiParser("a![b].0 | 0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.NotEmpty(result.DeadProcessDescriptions);
                Assert.Contains("Вероятно бесполезный", result.DeadProcessDescriptions[0]);
            }

            [Fact]
            public void Analyze_ChannelUsedBothWays_NotInInputOnlyOrOutputOnly()
            {
                var parser = new PiParser("x![y].0 | x?(z).0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("x", result.UsedChannels);
                Assert.DoesNotContain("x", result.InputOnlyChannels);
                Assert.DoesNotContain("x", result.OutputOnlyChannels);
            }

            [Fact]
            public void Analyze_ChannelUsedBothWays_NotInInputOnlyOrOutputOnl1y1()
            {
                var log = new System.Text.StringBuilder();
                var parser = new PiParser("x![z].0 | x?(y). y![x]. x?(y).0 | z?(v). v![v].0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);
                log.AppendLine(result.ToString());
            }

            [Fact]
            public void Analyze_ComplexProcess_CorrectlyIdentifiesAll()
            {
                var parser = new PiParser("{*a}a![b].0 | {*c}c?(d).0 | e![f].0 | g?(h).0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("a", result.DefinedChannels);
                Assert.Contains("c", result.DefinedChannels);
                Assert.Contains("a", result.UsedChannels);
                Assert.Contains("c", result.UsedChannels);
                Assert.Contains("e", result.UsedChannels);
                Assert.Contains("g", result.UsedChannels);

                Assert.Contains("a", result.OutputOnlyChannels);
                Assert.Contains("c", result.InputOnlyChannels);
                Assert.Contains("e", result.OutputOnlyChannels);
                Assert.Contains("g", result.InputOnlyChannels);

                Assert.Empty(result.UnusedDefinedChannels);
            }

            [Fact]
            public void Analyze_WithLambda_IgnoresVariables()
            {
                var parser = new PiParser("a![(fun x -> x) y].0 | b?(z).0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("a", result.UsedChannels);
                Assert.Contains("b", result.UsedChannels);
                Assert.DoesNotContain("x", result.UsedChannels); // x - переменная лямбды, не канал
                Assert.DoesNotContain("y", result.UsedChannels); // y - аргумент, тоже переменная
                Assert.DoesNotContain("z", result.UsedChannels); // z - переменная ввода
            }

            [Fact]
            public void Analyze_LetProcess_DoesNotCountVariablesAsChannels()
            {
                var parser = new PiParser("let res = (λx.x) arg.out![res].0");
                var process = parser.Parse();
                var result = PiAnalyzer.Analyze(process);

                Assert.Contains("out", result.UsedChannels);
                Assert.DoesNotContain("res", result.UsedChannels);
                Assert.DoesNotContain("arg", result.UsedChannels);
            }
        }

        public class SimulationTests
        {
            [Fact]
            public async Task Simulate_SimpleCompleteProcess_ReturnsNoDeadlock()
            {
                // Процесс: a![b].0 | a?(x).0
                var parser = new PiParser("a![b].0 | a?(x).0");
                var process = parser.Parse();
                var result = await PiAnalyzer.SimulateAsync(process);

                Assert.False(result.IsDeadlocked);
                Assert.Equal(1, result.StepsExecuted);
                Assert.Equal("0", result.FinalState);
                Assert.Empty(result.DeadlockedProcesses);
            }

            [Fact]
            public async Task Simulate_OnlyOutput_CompletesSuccessfully()
            {
                // Процесс: a![b].0 (один выход без входа) - выполнится за 1 шаг
                var parser = new PiParser("a![b].0");
                var process = parser.Parse();
                var result = await PiAnalyzer.SimulateAsync(process);

                Assert.False(result.IsDeadlocked);
                Assert.Equal(1, result.StepsExecuted);
                Assert.Equal("0", result.FinalState);
            }

            [Fact]
            public async Task Simulate_OnlyInput_Deadlock()
            {
                // Процесс: a?(x).0 (только вход без сообщений) - deadlock
                var parser = new PiParser("a?(x).0");
                var process = parser.Parse();
                var result = await PiAnalyzer.SimulateAsync(process);

                Assert.True(result.IsDeadlocked);
                Assert.Equal(0, result.StepsExecuted);
                Assert.Contains("a?(x).0", result.DeadlockedProcesses);
            }

            [Fact]
            public async Task Simulate_DeadlockAfterOneStep()
            {
                // Процесс: a![b].a?(x).0 | a?(y).0
                // Первый шаг: a![b] и a?(y) сопоставляются → остаётся a?(x).0, который deadlock
                var parser = new PiParser("a![b].a?(x).0 | a?(y).0");
                var process = parser.Parse();
                var result = await PiAnalyzer.SimulateAsync(process);

                Assert.True(result.IsDeadlocked);
                Assert.Equal(1, result.StepsExecuted);
                Assert.Contains("a?(x).0", result.DeadlockedProcesses);
                Assert.DoesNotContain("a?(y).0", result.DeadlockedProcesses);
            }

            [Fact]
            public async Task Simulate_WikipediaExample_NoDeadlock()
            {
                // Пример из Википедии: x![z].0 | x?(y).y![x].x?(y).0 | z?(v).v![v].0
                // Должен выполниться за 3 шага без deadlock
                var expression = "x![z].0 | x?(y).y![x].x?(y).0 | z?(v).v![v].0";
                var parser = new PiParser(expression);
                var process = parser.Parse();
                var result = await PiAnalyzer.SimulateAsync(process);

                Assert.False(result.IsDeadlocked);
                Assert.Equal(3, result.StepsExecuted);
                Assert.Equal("0", result.FinalState);
                Assert.Empty(result.DeadlockedProcesses);
            }

            [Fact]
            public async Task Simulate_BroadcastProcess_NoDeadlock()
            {
                // Broadcast с двумя получателями: a!![hello].0 | a?(x).out1![x].0 | a?(y).out2![y].0
                // После broadcast останутся out1![hello].0 и out2![hello].0, они выполнятся за два шага.
                var expression = "a!![hello].0 | a?(x).out1![x].0 | a?(y).out2![y].0";
                var parser = new PiParser(expression);
                var process = parser.Parse();
                var result = await PiAnalyzer.SimulateAsync(process);

                Assert.False(result.IsDeadlocked);
                Assert.Equal(2, result.StepsExecuted); // broadcast + два выхода
                Assert.Equal("0", result.FinalState);
            }

            [Fact]
            public async Task Simulate_RespectsMaxSteps()
            {
                // Процесс, который завершается за 1 шаг, но лимит шагов = 1
                var expression = "a![b].0 | a?(x).0";
                var parser = new PiParser(expression);
                var process = parser.Parse();
                var result = await PiAnalyzer.SimulateAsync(process, maxSteps: 1);

                Assert.False(result.IsDeadlocked);
                Assert.Equal(1, result.StepsExecuted);
                Assert.Equal("0", result.FinalState); // процесс завершился
            }

            [Fact]
            public async Task FourInputs_Deadlock_Detected()
            {
                // Процесс: 4 входа на разных каналах без выходов и без сообщений в каналах
                var parser = new PiParser("a?(x).0 | b?(y).0 | c?(z).0 | d?(w).0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result = await session.ExecuteStepAsync();

                // Проверяем, что deadlock обнаружен
                Assert.True(result.IsDeadlocked, "Процесс должен быть в deadlock");

                // Должны быть заблокированы все 4 входа
                Assert.Equal(4, result.DeadlockedInputs.Count);
                Assert.Contains("a?(x).0", result.DeadlockedInputs);
                Assert.Contains("b?(y).0", result.DeadlockedInputs);
                Assert.Contains("c?(z).0", result.DeadlockedInputs);
                Assert.Contains("d?(w).0", result.DeadlockedInputs);

                // Процесс не завершён
                Assert.False(result.IsCompleted);

                // Состояние должно содержать все 4 входа
                Assert.Contains("a?(x).0", result.CurrentState);
                Assert.Contains("b?(y).0", result.CurrentState);
                Assert.Contains("c?(z).0", result.CurrentState);
                Assert.Contains("d?(w).0", result.CurrentState);
            }
        }























        //         [Fact]
        //         public void ParseNullProcess()
        //         {
        //             var parser = new PiParser("0");
        //             var result = parser.Parse();

        //             Assert.IsType<NullProcess>(result);
        //             Assert.Equal("0", result.ToString());
        //         }

        //         [Fact]
        //         public void ParseOutputProcess()
        //         {
        //             var parser = new PiParser("x![y].0");
        //             var result = parser.Parse();

        //             var output = Assert.IsType<OutputProcess>(result);
        //             Assert.Equal("x", output.Channel);
        //             Assert.Equal("y", output.Message);
        //             Assert.IsType<NullProcess>(output.Continuation);
        //         }

        //         [Fact]
        //         public void ParseInputProcess()
        //         {
        //             var parser = new PiParser("a?(b).0");
        //             var result = parser.Parse();

        //             var input = Assert.IsType<InputProcess>(result);
        //             Assert.Equal("a", input.Channel);
        //             Assert.Equal("b", input.Variable);
        //             Assert.IsType<NullProcess>(input.Continuation);
        //         }

        //         [Fact]
        //         public void ParseParallelProcess()
        //         {
        //             var parser = new PiParser("x![y].0 | a?(b).0");
        //             var result = parser.Parse();

        //             var parallel = Assert.IsType<ParallelProcess>(result);
        //             var processes = parallel.Processes;

        //             Assert.Equal(2, processes.Count);
        //             Assert.IsType<OutputProcess>(processes[0]);
        //             Assert.IsType<InputProcess>(processes[1]);

        //             // Дополнительные проверки
        //             var output = (OutputProcess)processes[0];
        //             Assert.Equal("x", output.Channel);
        //             Assert.Equal("y", output.Message);

        //             var input = (InputProcess)processes[1];
        //             Assert.Equal("a", input.Channel);
        //             Assert.Equal("b", input.Variable);
        //         }

        //         [Fact]
        //         public void ParsesRestrictionWithBraces()
        //         {
        //             var input = "{*x}x![y].0";
        //             var parser = new PiParser(input);
        //             var result = parser.Parse();

        //             var restriction = Assert.IsType<RestrictionProcess>(result);
        //             Assert.Equal("x", restriction.Name);
        //             Assert.IsType<OutputProcess>(restriction.Body);
        //         }


        //         //[Fact]
        //         //public void ParseNestedProcesses()
        //         //{
        //         //    var parser = new PiParser("(?x)(x![z].0 | a?(b).b![x].0) | c?(d).0");
        //         //    var result = parser.Parse();

        //         //    var parallel = Assert.IsType<ParallelProcess>(result);
        //         //    var restriction = Assert.IsType<RestrictionProcess>(parallel.Left);
        //         //    var innerParallel = Assert.IsType<ParallelProcess>(restriction.Body);
        //         //}

        //         [Fact]
        //         public void ParseComplexExample()
        //         {
        //             var input = "x![z].0 | x?(y).y![z].0 | z?(v).v![v].0";
        //             var parser = new PiParser(input);
        //             var result = parser.Parse();

        //             var parallel = Assert.IsType<ParallelProcess>(result);
        //             Assert.Equal(2, parallel.Processes.Count);

        //             var restriction = Assert.IsType<RestrictionProcess>(parallel.Processes[0]);
        //             Assert.Equal("x", restriction.Name);

        //             var innerParallel = Assert.IsType<ParallelProcess>(restriction.Body);
        //             Assert.Equal(2, innerParallel.Processes.Count);

        //             var inputProcess = Assert.IsType<InputProcess>(parallel.Processes[1]);
        //             InputProcess p1 = (InputProcess) parallel.Processes[1];
        //             Assert.IsType<OutputProcess>(p1.Continuation);
        //             Assert.Equal("z", inputProcess.Channel);
        //         }

        //         [Fact]
        //         public void ThrowsOnInvalidSyntax_MissingBracket()
        //         {
        //             var parser = new PiParser("x![y.0");
        //             Assert.Throws<Exception>(() => parser.Parse());
        //         }

        //         [Fact]
        //         public void ThrowsOnInvalidSyntax_UnknownSymbol()
        //         {
        //             var parser = new PiParser("x@[y].0");
        //             Assert.Throws<Exception>(() => parser.Parse());
        //         }

        //         [Fact]
        //         public void ThrowsOnEmptyInput()
        //         {
        //             Assert.Throws<ArgumentException>(() => new PiParser(""));
        //         }

        //         [Fact]
        //         public void ToString_ProducesCorrectOutput()
        //         {
        //             var process = new ParallelProcess(new List<Process>
        //                 {
        //                     new OutputProcess("x", "y", new NullProcess()),
        //                     new InputProcess("a", "b", new NullProcess())
        //                 });

        //             Assert.Equal("(x![y].0 | a?(b).0)", process.ToString());
        //         }

        //         [Fact]
        //         public void ParsesLambdaInOutput()
        //         {
        //             var input = "a![λx.x].0";
        //             var result = new PiParser(input).Parse();

        //             var output = Assert.IsType<OutputProcess>(result);
        //             Assert.Equal("λx.x", output.Message);
        //         }

        //         [Fact]
        //         public void ParsesLetExpression()
        //         {
        //             var input = "let z = (λx.x) y.0";
        //             var result = new PiParser(input).Parse();

        //             var let = Assert.IsType<LetProcess>(result);
        //             Assert.Equal("z", let.ResultVar);      
        //             Assert.Equal("λx.x", let.Lambda.ToString()); 
        //             Assert.Equal("y", let.ArgumentVar);    
        //         }

        //         [Fact]
        //         public async Task LegacyBehavior_StillWorks()
        //         {
        //             // a?(x).b![x].0 | a!["test"]
        //             var process = new ParallelProcess(new List<Process> {
        //                 new InputProcess("a", "x", new OutputProcess("b", "x", new NullProcess())),
        //                 new OutputProcess("a", "test", new NullProcess())
        //             });

        //             var runtime = new PiRuntime(process);
        //             var result = await runtime.ExecuteStepAsync();

        //             Assert.Contains("Sent 'test' via a", result.ParallelActions);
        //         }
    }
}






