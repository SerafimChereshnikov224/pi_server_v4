using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.runtime;
using Xunit;
using System.Diagnostics; 


namespace PiServer.version_2.interpreter.tests
{
    public class PiParserTests
    {

        public class ParserTests
        {
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
                Assert.False(result3.IsCompleted);
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

            [Fact]
            public async Task ExecuteStep_NestedCommunicationChain()
            {
                var parser = new PiParser("a?(x).x![response].0 | a![b].0 | b?(y).y![final].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result1 = await session.ExecuteStepAsync();
                Assert.False(result1.IsCompleted);
                Assert.Contains("b![response].0", result1.CurrentState);

                var result2 = await session.ExecuteStepAsync();
                Assert.False(result2.IsCompleted);
                Assert.Contains("response![final].0", result2.CurrentState);

                var result3 = await session.ExecuteStepAsync();
                Assert.False(result3.IsCompleted);

                var result4 = await session.ExecuteStepAsync();
                Assert.True(result4.IsCompleted);
            }

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
                var parser = new PiParser("a![(fun x -> x * 2) 5].0 | a?(y). out![y].0");
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                var result1 = await session.ExecuteStepAsync();
                var result2 = await session.ExecuteStepAsync();
                var result3 = await session.ExecuteStepAsync();

                Assert.True(result3.IsCompleted);
                Assert.Contains("10", result2.CurrentState); // 5 * 2 = 10
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
                var result3 = await session.ExecuteStepAsync();

                Assert.True(result3.IsCompleted);
                Assert.Contains("hello", result2.CurrentState);
                Assert.DoesNotContain("λx.x", result2.CurrentState);
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

                Assert.False(result.IsCompleted);
                Assert.Contains("a![message].0", result.CurrentState);
                Assert.Contains("b![another].0", result.CurrentState);
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

        public class IntegrationScenariosTests
        {
            [Theory]
            [InlineData("a![b].0 | a?(x).0", 1, "Completed")] 
            [InlineData("a![b].0 | a?(x).x![c].0 | b?(y).0", 2, "Completed")] 
            // [InlineData("a?(x).0", 1, "Deadlock")] // Deadlock
            // [InlineData("a![b].0 | c![d].0", 1, "Completed")] 
            [InlineData("a![b].c![d].0 | a?(x).c?(y).0", 2, "Completed")] 
            public async Task VariousScenarios_BehaveAsExpected(string processDefinition, int expectedSteps, string expectedStatus)
            {
                var parser = new PiParser(processDefinition);
                var process = parser.Parse();
                var session = new PiRuntimeSession(process);

                StepResult result = null;
                int actualSteps = 0;

                do
                {
                    result = await session.ExecuteStepAsync();
                    actualSteps++;
                } while (!result.IsCompleted && actualSteps < 10);

                Assert.True(result.IsCompleted);
                Assert.True(actualSteps <= expectedSteps, $"Expected at most {expectedSteps} steps, but took {actualSteps}");
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






