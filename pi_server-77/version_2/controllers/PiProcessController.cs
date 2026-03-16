using Microsoft.AspNetCore.Mvc;
using PiServer.Services;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.models;
using PiServer.version_2.runtime;
using System;
using System.Collections.Concurrent;
using PiServer.version_2.analyzer;

namespace PiServer.version_2.controllers
{
    [ApiController]
    [Route("api/pi")]
    public class PiProcessController : ControllerBase
    {
        internal static readonly ConcurrentDictionary<string, PiRuntimeSession> _sessions = new();

        // --- [ 1. Запуск нового процесса ] ---
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

        // --- [ 2. Один шаг вычисления ] ---
        [HttpPost("{sessionId}/step")]
        public async Task<IActionResult> ExecuteStep(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

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

        // --- [ 3. Получить текущее состояние процесса ] ---
        [HttpGet("{sessionId}")]
        public IActionResult GetState(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

            return Ok(new ProcessState
            {
                CurrentState = session.CurrentProcess.ToString(),
                IsCompleted = session.IsCompleted
            });
        }

        // --- [ 4. Лямбда-вычисления ] ---
        [HttpPost("evaluate")]
        public IActionResult EvaluateLambda([FromBody] LambdaRequest request)
        {
            try
            {
                var result = LambdaEvaluator.EvaluateLambda(request.Expression);
                return Ok(new { Result = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        // --- [ 5. Режим обучения ] ---
        [HttpPost("learning/start")]
        public IActionResult StartLearningSession([FromBody] LearningRequest request)
        {
            try
            {
                var parser = new PiParser(request.ProcessDefinition);
                var process = parser.Parse();

                var mode = request.Mode?.ToLower() == "learning"
                    ? LearningMode.Learning
                    : LearningMode.Auto;

                var sessionId = Guid.NewGuid().ToString();
                _sessions[sessionId] = new PiRuntimeSession(process, mode);

                var hint = mode == LearningMode.Learning
                    ? "Введите следующий шаг вычисления"
                    : "Автоматический режим";

                return Ok(new
                {
                    SessionId = sessionId,
                    CurrentState = process.ToString(),
                    Mode = mode.ToString(),
                    Hint = hint,
                    ExpectedNextStep = mode == LearningMode.Learning
                        ? GetExpectedFirstStep(process)
                        : null
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        // --- [ 6. Шаг в обучающем режиме ] ---
        [HttpPost("{sessionId}/learning/step")]
        public async Task<IActionResult> ExecuteLearningStep(
            string sessionId,
            [FromBody] StepVerificationRequest request)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

            if (session.Mode != LearningMode.Learning)
                return BadRequest(new { Error = "Session is not in learning mode" });

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

        // --- [ 7. Подсказка для текущего шага обучения ] ---
        [HttpGet("{sessionId}/learning/hint")]
        public IActionResult GetLearningHint(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

            if (session.Mode != LearningMode.Learning)
                return BadRequest(new { Error = "Session is not in learning mode" });

            return Ok(new
            {
                Hint = session.GetCurrentHint(),
                ExpectedNextStep = session.GetCurrentExpectedStep()
            });
        }

        // --- [ 8. Автоматический шаг в режиме обучения ] ---
        [HttpPost("{sessionId}/learning/auto-step")]
        public async Task<IActionResult> ExecuteAutoStep(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

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

        // --- [ 9. Переключение режима обучения (заглушка) ] ---
        [HttpPost("{sessionId}/learning/switch-mode")]
        public IActionResult SwitchLearningMode(string sessionId, [FromBody] string mode)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

            return Ok(new
            {
                Message = "Для смены режима создайте новую сессию",
                CurrentMode = session.Mode.ToString()
            });
        }

        // --- [ 10. Получить описание текущего шага обучения ] ---
        [HttpGet("{sessionId}/learning/description")]
        public IActionResult GetStepDescription(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

            if (session.Mode != LearningMode.Learning)
                return BadRequest(new { Error = "Session is not in learning mode" });

            return Ok(new
            {
                Description = session.GetStepDescription(),
                ExpectedExpression = session.GetCurrentExpectedStep()
            });
        }

        // --- [ 11. Проверить статус сессии обучения ] ---
        [HttpGet("{sessionId}/learning/status")]
        public IActionResult GetLearningStatus(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return NotFound(new { Error = "Session not found" });

            if (session.Mode != LearningMode.Learning)
                return BadRequest(new { Error = "Session is not in learning mode" });

            return Ok(new
            {
                RequiresInput = session.RequiresUserInput(),
                IsCompleted = session.IsCompleted,
                CurrentState = session.CurrentProcess.ToString(),
                Hint = session.GetCurrentHint(),
                ExpectedNextStep = session.GetCurrentExpectedStep()
            });
        }

        // --- [ 11. Анализ введенного выражения ] ---
        [HttpPost("analyze")]
        public IActionResult AnalyzeProcess([FromBody] ProcessRequest request)
        {
            try
            {
                var parser = new PiParser(request.ProcessDefinition);
                var process = parser.Parse();
                var analysis = PiAnalyzer.Analyze(process);
                return Ok(analysis);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        // --- [ Helper: ожидание первого шага в обучении ] ---
        private string GetExpectedFirstStep(Process process)
        {
            return process switch
            {
                OutputProcess op => $"Send {op.Message} via {op.Channel}",
                InputProcess ip => $"Receive from {ip.Channel}",
                ParallelProcess => "Параллельное выполнение",
                LetProcess lp => $"Compute {lp.ResultVar} = {lp.Lambda}",
                _ => "Начните вычисление"
            };
        }
    }
}
