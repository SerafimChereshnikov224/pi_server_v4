using Microsoft.AspNetCore.Mvc;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.models; // Добавьте этот using
using PiServer.version_2.runtime;
using System.Collections.Concurrent;
using PiServer.Services; 

namespace PiServer.version_2.controllers
{
    using Microsoft.AspNetCore.Mvc;
    using PiServer.version_2.interpreter.core.parser;
    using PiServer.version_2.interpreter.core.syntax;
    using System.Collections.Concurrent;

    [ApiController]
    [Route("api/pi")]
    public class PiProcessController : ControllerBase
    {
        internal static readonly ConcurrentDictionary<string, PiRuntimeSession> _sessions = new();

        [HttpPost("start")]
        public IActionResult StartProcess([FromBody] ProcessRequest request)
        {
            try
            {
                var parser = new PiParser(request.ProcessDefinition);
                var process = parser.Parse();

                var sessionId = Guid.NewGuid().ToString();
                _sessions[sessionId] = new PiRuntimeSession(process);

                return Ok(new ProcessResponse
                {
                    SessionId = sessionId,
                    CurrentState = process.ToString()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{sessionId}/step")]
        public async Task<IActionResult> ExecuteStep(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound();

            try
            {
                var result = await session.ExecuteStepAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("{sessionId}")]
        public IActionResult GetState(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound();

            return Ok(new ProcessState
            {
                CurrentState = session.CurrentProcess.ToString(),
                IsCompleted = session.IsCompleted
            });
        }

        [HttpPost("evaluate")]
        public IActionResult EvaluateLambda([FromBody] LambdaRequest request)
        {
            try
            {
                var result = LambdaEvaluator.EvaluateLambda(request.Expression);
                return Ok(new { result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // НОВЫЕ ENDPOINT'Ы ДЛЯ ОБУЧЕНИЯ

        [HttpPost("learning/start")]
        public IActionResult StartLearningSession([FromBody] LearningRequest request)
        {
            try
            {
                var parser = new PiParser(request.ProcessDefinition);
                var process = parser.Parse();

                var mode = request.Mode?.ToLower() == "learning" ? 
                    LearningMode.Learning : LearningMode.Auto;

                var sessionId = Guid.NewGuid().ToString();
                _sessions[sessionId] = new PiRuntimeSession(process, mode);

                var hint = mode == LearningMode.Learning ? 
                    "Введите следующий шаг вычисления" : "Автоматический режим";

                return Ok(new 
                { 
                    SessionId = sessionId,
                    CurrentState = process.ToString(),
                    Mode = mode.ToString(),
                    Hint = hint,
                    ExpectedNextStep = mode == LearningMode.Learning ? 
                        GetExpectedFirstStep(process) : null
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{sessionId}/learning/step")]
        public async Task<IActionResult> ExecuteLearningStep(
            string sessionId, 
            [FromBody] StepVerificationRequest request)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound("Session not found");

            if (session.Mode != LearningMode.Learning)
                return BadRequest("Session is not in learning mode");

            try
            {
                var result = await session.ExecuteLearningStepAsync(request.UserInput);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("{sessionId}/learning/hint")]
        public IActionResult GetLearningHint(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound("Session not found");

            if (session.Mode != LearningMode.Learning)
                return BadRequest("Session is not in learning mode");

            return Ok(new { 
                Hint = session.GetCurrentHint(),
                ExpectedNextStep = session.GetCurrentExpectedStep()
            });
        }

        [HttpPost("{sessionId}/learning/auto-step")]
        public async Task<IActionResult> ExecuteAutoStep(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound("Session not found");

            try
            {
                var result = await session.ExecuteStepAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{sessionId}/learning/switch-mode")]
        public IActionResult SwitchLearningMode(string sessionId, [FromBody] string mode)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound("Session not found");

            return Ok(new
            {
                Message = "Для смены режима создайте новую сессию",
                CurrentMode = session.Mode.ToString()
            });
        }


        [HttpGet("{sessionId}/learning/description")]

        public IActionResult GetStepDescription(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound("Session not found");

            if (session.Mode != LearningMode.Learning)
                return BadRequest("Session is not in learning mode");

            return Ok(new
            {
                Description = session.GetStepDescription(),
                ExpectedExpression = session.GetCurrentExpectedStep()
            });
        }
        // В контроллер добавляем endpoint для проверки статуса

        [HttpGet("{sessionId}/learning/status")]

        public IActionResult GetLearningStatus(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound("Session not found");

            if (session.Mode != LearningMode.Learning)
                return BadRequest("Session is not in learning mode");

            return Ok(new
            {
                RequiresInput = session.RequiresUserInput(),
                IsCompleted = session.IsCompleted,
                CurrentState = session.CurrentProcess.ToString(),
                Hint = session.GetCurrentHint(),
                ExpectedNextStep = session.GetCurrentExpectedStep()
            });
        }

        private string GetExpectedFirstStep(Process process)
        {
            return process switch
            {
                OutputProcess op => op.Message,
                InputProcess ip => $"Receive from {ip.Channel}",
                ParallelProcess pp => "Параллельное выполнение",
                _ => "Начните вычисление"
            };
        }
    }
}