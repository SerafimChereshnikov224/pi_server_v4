


using Microsoft.AspNetCore.Mvc;
using PiServer.Models;
using PiServer.Services;
using System.Text;
using System.Collections.Concurrent;

namespace PiServer.Controllers
{
    [ApiController]
    [Route("api/builder")]
    public class BuilderController : ControllerBase
    {
        private readonly EnvironmentManager _environment;

        private static ProcessBuilder _builder;
        private static readonly StringBuilder _log = new();

        public BuilderController(EnvironmentManager environment)
        {
            _environment = environment;
            _builder ??= new ProcessBuilder(environment);
        }

        [HttpPost("reset")]
        public IActionResult Reset()
        {
            _builder = new ProcessBuilder(_environment);
            return Ok("Состояние сброшено.");
        }



        [HttpGet("logs")]
        public IActionResult GetLogs()
        {
            return Ok(new
            {
                logs = _environment.MessageLogs
            });
        }



        [HttpPost("send")]
        public IActionResult AddSend([FromBody] SendDto dto)
        {
            _environment.LogMessage($"Received AddSend: channel={dto.Channel}, message={dto.Message}");
            _environment.GetOrCreateChannel(dto.Channel, ChannelStrategy.PassiveEnvironment);


            if (dto.Continuation != null)
            {
                var continuation = ProcessBuilder.BuildFromDto(dto.Continuation);
                _builder.AddSend(dto.Channel, dto.Message, continuation);
            }
            else
            {
                _builder.AddSend(dto.Channel, dto.Message);
            }


            return Ok("Send добавлен.");
        }

        [HttpPost("receive")]
        public IActionResult AddReceive([FromBody] ReceiveDto dto)
        {
            _environment.GetOrCreateChannel(dto.Channel, ChannelStrategy.PassiveEnvironment);


            if (dto.Continuation != null)
            {
                var continuation = ProcessBuilder.BuildFromDto(dto.Continuation);
                _builder.AddReceive(dto.Channel, dto.Filter, _ => continuation);
            }
            else
            {
                _builder.AddReceive(dto.Channel, dto.Filter, msg =>
                {
                    Console.WriteLine($"Получено: {msg}");
                    return new InactiveProcess();
                });
            }


            return Ok("Receive добавлен.");
        }

        [HttpPost("parallel")]
        public IActionResult AddParallel([FromBody] ParallelDto dto)
        {
            var processes = dto.Processes.Select(ProcessBuilder.BuildFromDto).ToArray();
            _builder.AddParallel(processes);

            return Ok("Parallel добавлен.");
        }

        [HttpPost("inactive")]
        public IActionResult AddInactive()
        {
            _builder.AddInactive();
            return Ok("Inactive добавлен.");
        }

        [HttpGet("diagram")]
        public IActionResult GetDiagram()
        {
            return Ok(_builder.GetProcessDiagram());
        }




        [HttpPost("execute")]
        public async Task<IActionResult> Execute()
        {
            try
            {
                _log.Clear();
                _log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] START");

                await _builder.ExecuteAsync();

                _log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] END");

                return Ok(new
                {
                    Diagram = _builder.GetProcessDiagram(),
                    Log = _log.ToString(),
                    Messages = _builder.GetMessageLogs() 

                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }
    }

    // DTOs
    public class SendDto
    {
        public string Channel { get; set; } = "";
        public string Message { get; set; } = "";
        public ProcessDto? Continuation { get; set; }
    }

    public class ReceiveDto
    {
        public string Channel { get; set; } = "";
        public string Filter { get; set; } = "";
        public ProcessDto? Continuation { get; set; }
    }

    public class ParallelDto
    {
        public List<ProcessDto> Processes { get; set; } = new();
    }

    public class ProcessDto
    {
        public string Type { get; set; } = "";
        public string Channel { get; set; } = "";
        public string Message { get; set; } = "";
        public string Filter { get; set; } = "";
        public List<ProcessDto>? Processes { get; set; }
        public ProcessDto? Continuation { get; set; }

    }
}
