using Microsoft.AspNetCore.Mvc;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.models;
using PiServer.version_2.runtime;
using System.Collections.Concurrent;

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
    }

    public class ProcessRequest
    {
        public string ProcessDefinition { get; set; }
    }

    public class ProcessResponse
    {
        public string SessionId { get; set; }
        public string CurrentState { get; set; }
    }

    public class ProcessState
    {
        public string CurrentState { get; set; }
        public bool IsCompleted { get; set; }
    }
}
