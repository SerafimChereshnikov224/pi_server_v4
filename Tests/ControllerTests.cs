using Microsoft.AspNetCore.Mvc;
using PiServer.version_2.controllers;
using PiServer.version_2.runtime;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    using Xunit;
    using System.Threading.Tasks;

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

            //[Fact]
            //public async Task ExecuteStep_WithOutputProcess_CompletesAfterOneStep()
            //{
            //    // Arrange
            //    var startResult = _controller.StartProcess(new ProcessRequest
            //    {
            //        ProcessDefinition = "x![y].0"
            //    }) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    var sessionId = (startResult.Value as ProcessResponse).SessionId;

            //    // Act
            //    var stepResult = await _controller.ExecuteStep(sessionId) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    var stepResponse = stepResult.Value as StepResult;

            //    // Assert
            //    Assert.Equal("0", stepResponse.CurrentState);
            //    Assert.True(stepResponse.IsCompleted);
            //}

            //[Fact]
            //public async Task ExecuteStep_WithParallelProcess_ExecutesBothBranches()
            //{
            //    // Arrange
            //    var startResult = _controller.StartProcess(new ProcessRequest
            //    {
            //        ProcessDefinition = "x![y].0 | z![w].0"
            //    }) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    var sessionId = (startResult.Value as ProcessResponse).SessionId;

            //    // Act - First step
            //    var step1 = await _controller.ExecuteStep(sessionId) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    var response1 = step1.Value as StepResult;

            //    // Assert - After first step
            //    Assert.Contains("x![y].0", response1.CurrentState);
            //    Assert.Contains("z![w].0", response1.CurrentState);

            //    // Act - Second step
            //    var step2 = await _controller.ExecuteStep(sessionId) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    var response2 = step2.Value as StepResult;

            //    // Assert - After second step
            //    Assert.Equal("0 | 0", response2.CurrentState);
            //}

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

                // Assert
                // Проверяем, что коммуникация произошла
                Assert.Equal(1, result.ParallelActions.Count);
                Assert.Contains("Sent 'hello' via x", result.ParallelActions);

                // Проверяем новое состояние
                Assert.Equal("(0 | 0)", result.CurrentState);

                // Проверяем описание действия
                Assert.Equal("Parallel step (1 actions)", result.LastAction);

                // Процесс должен быть завершен, так как все подпроцессы - NullProcess
                Assert.True(result.IsCompleted);

            }

            //[Fact]
            //public async Task ExecuteStep_WithComplexProcess_ExecutesCorrectly()
            //{
            //    // Arrange
            //    var startResult = _controller.StartProcess(new ProcessRequest
            //    {
            //        ProcessDefinition = "(νx)(x![z].0 | x?(y).y![x].0) | z?(v).v![v].0"
            //    }) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    var sessionId = (startResult.Value as ProcessResponse).SessionId;

            //    // Act & Assert - Step 1
            //    var step1 = await _controller.ExecuteStep(sessionId) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    Assert.Contains("(νx)(0 | z![x].0)", (step1.Value as StepResult).CurrentState);

            //    // Act & Assert - Step 2
            //    var step2 = await _controller.ExecuteStep(sessionId) as Microsoft.AspNetCore.Mvc.OkObjectResult;
            //    Assert.Contains("(νx)(0 | x![x].0)", (step2.Value as StepResult).CurrentState);
            //}
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
