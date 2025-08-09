using Microsoft.AspNetCore.Mvc;
using PiServer.version_2.controllers;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    using Microsoft.VisualStudio.TestPlatform.Utilities;
    using System.Threading.Tasks;
    using Xunit;

    namespace PiServer.Tests
    {
        public class PiProcessControllerTests
        {
            private readonly PiProcessController _controller = new PiProcessController();

            [Fact]
            public void StartProcess_WithNullProcess_ReturnsOk()
            {
                // Arrange
                var request = new ProcessRequest { ProcessDefinition = "0" };

                // Act
                var result = _controller.StartProcess(request);

                // Assert
                Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
                var response = (result as Microsoft.AspNetCore.Mvc.OkObjectResult).Value as ProcessResponse;
                Assert.Equal("0", response.CurrentState);
            }


            [Fact]
            public void GetState_WithInvalidSession_ReturnsNotFound()
            {
                // Act
                var result = _controller.GetState("invalid-session-id");

                // Assert
                Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
            }

            [Fact]
            public async Task ParallelProcess_ShouldExecuteCommunicationsInOneStep()
            {
                List<Process> proList = new List<Process>();
                proList.Add(new OutputProcess("x", "hello", new NullProcess()));
                proList.Add(new InputProcess("x", "msg", new NullProcess()));
                // Arrange
                var process = new ParallelProcess(
                    proList
                );
                var runtime = new PiRuntime(process);

                // Act
                var result = await runtime.ExecuteStepAsync();

                Assert.Equal(1, result.ParallelActions.Count);
                Assert.Contains("Sent 'hello' via x", result.ParallelActions);

                Assert.Equal("(0 | 0)", result.CurrentState);

                Assert.Equal("Parallel step (1 actions)", result.LastAction);

                Assert.True(result.IsCompleted);

            }

            [Fact]
            public async Task LetProcess_InParallel_ComputesCorrectly()
            {
                // Arrange a![hello] | a?(x). let z = (λx.x) x . out![z] 
                var process = new ParallelProcess(new List<Process>
                {
                    new InputProcess("a", "x",
                        new LetProcess("z", new LambdaAbs("x", new LambdaVar("x")), "x",
                        new OutputProcess("out", "z", new NullProcess()))),
                    new OutputProcess("a", "hello", new NullProcess())
                });

                var runtime = new PiRuntime(process);

                // Шаг 1: Должен выполнить коммуникацию a!["hello"] -> a?(x)
                var step1 = await runtime.ExecuteStepAsync();
                Assert.Single(step1.ParallelActions);
                Assert.Contains("Sent 'hello' via a", step1.ParallelActions);

                // Шаг 2: Должен вычислить let z = (λx.x) x
                var step2 = await runtime.ExecuteStepAsync();
                Assert.Single(step2.ParallelActions);
                Assert.Contains("Computed z = λx.x", step2.ParallelActions);

                var zValue = runtime._env.GetVariable("z");
                Console.WriteLine($"Значение z: {zValue}"); 
                                                            
                Console.WriteLine($"Value of z: {runtime._env.GetVariable("z")}");
                Console.WriteLine($"Current process: {runtime.CurrentProcess}");
                Console.WriteLine($"Enviroment: {string.Join(", ", runtime._env._variables.Select(v => $"{v.Key}={v.Value}"))}");

                var step3 = await runtime.ExecuteStepAsync();
                Assert.Equal("Sent 'hello' to out", step3.LastAction);
                Assert.True(step3.ParallelActions.Count == 0); 

                Console.WriteLine($"Sent: {step3.LastAction}"); // Должно быть "Sent 'hello' to out"
            }

        }


        public class StepResult
        {
            public string CurrentState { get; set; }
            public string LastAction { get; set; }
            public bool IsCompleted { get; set; }
            public List<string> ParallelActions { get; set; } = new List<string>();
        }
    }
}
