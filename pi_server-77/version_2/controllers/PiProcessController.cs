using PiServer.Services;
using PiServer.version_2.interpreter.core.parser;
using PiServer.version_2.interpreter.core.syntax;
using PiServer.version_2.models;
using PiServer.version_2.runtime;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System;

namespace PiServer.version_2.controllers
{
    public class PiProcessApi
    {
        internal static readonly ConcurrentDictionary<string, PiRuntimeSession> _sessions = new();

        public ProcessResponse StartProcess(ProcessRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ProcessDefinition))
                throw new ArgumentException("Process definition is empty", nameof(request));

            var parser = new PiParser(request.ProcessDefinition);
            var process = parser.Parse();

            var sessionId = Guid.NewGuid().ToString("N");
            _sessions[sessionId] = new PiRuntimeSession(process);

            return new ProcessResponse
            {
                SessionId = sessionId,
                CurrentState = process.ToString()
            };
        }

        public async Task<object> ExecuteStepAsync(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            var result = await session.ExecuteStepAsync();
            return result;
        }

        public ProcessState GetState(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            return new ProcessState
            {
                CurrentState = session.CurrentProcess.ToString(),
                IsCompleted = session.IsCompleted
            };
        }

        public object EvaluateLambda(LambdaRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Expression))
                throw new ArgumentException("Expression is empty", nameof(request));

            return LambdaEvaluator.EvaluateLambda(request.Expression);
        }

        public LearningStartResponse StartLearningSession(LearningRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ProcessDefinition))
                throw new ArgumentException("Process definition is empty", nameof(request));

            var parser = new PiParser(request.ProcessDefinition);
            var process = parser.Parse();

            var mode = request.Mode?.ToLower() == "learning"
                ? LearningMode.Learning
                : LearningMode.Auto;

            var sessionId = Guid.NewGuid().ToString("N");
            _sessions[sessionId] = new PiRuntimeSession(process, mode);

            var hint = mode == LearningMode.Learning
                ? "Введите следующий шаг вычисления"
                : "Автоматический режим";

            return new LearningStartResponse
            {
                SessionId = sessionId,
                CurrentState = process.ToString(),
                Mode = mode.ToString(),
                Hint = hint,
                ExpectedNextStep = mode == LearningMode.Learning ? GetExpectedFirstStep(process) : null
            };
        }

        public async Task<object> ExecuteLearningStepAsync(string sessionId, StepVerificationRequest request)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            if (session.Mode != LearningMode.Learning)
                throw new InvalidOperationException("Session is not in learning mode");

            var result = await session.ExecuteLearningStepAsync(request?.UserInput);
            return result;
        }

        public LearningHintResponse GetLearningHint(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            if (session.Mode != LearningMode.Learning)
                throw new InvalidOperationException("Session is not in learning mode");

            return new LearningHintResponse
            {
                Hint = session.GetCurrentHint(),
                ExpectedNextStep = session.GetCurrentExpectedStep()
            };
        }

        public async Task<object> ExecuteAutoStepAsync(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            var result = await session.ExecuteStepAsync();
            return result;
        }

        public LearningSwitchModeResponse SwitchLearningMode(string sessionId, string mode)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            return new LearningSwitchModeResponse
            {
                Message = "Для смены режима создайте новую сессию",
                CurrentMode = session.Mode.ToString()
            };
        }

        public LearningDescriptionResponse GetStepDescription(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            if (session.Mode != LearningMode.Learning)
                throw new InvalidOperationException("Session is not in learning mode");

            return new LearningDescriptionResponse
            {
                Description = session.GetStepDescription(),
                ExpectedExpression = session.GetCurrentExpectedStep()
            };
        }

        public LearningStatusResponse GetLearningStatus(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                throw new KeyNotFoundException("Session not found");

            if (session.Mode != LearningMode.Learning)
                throw new InvalidOperationException("Session is not in learning mode");

            return new LearningStatusResponse
            {
                RequiresInput = session.RequiresUserInput(),
                IsCompleted = session.IsCompleted,
                CurrentState = session.CurrentProcess.ToString(),
                Hint = session.GetCurrentHint(),
                ExpectedNextStep = session.GetCurrentExpectedStep()
            };
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

    public class LambdaRequest
    {
        public string Expression { get; set; }
    }

    public class LearningRequest
    {
        public string ProcessDefinition { get; set; }
        public string Mode { get; set; }
    }

    public class StepVerificationRequest
    {
        public string UserInput { get; set; }
    }

    public class LearningStartResponse
    {
        public string SessionId { get; set; }
        public string CurrentState { get; set; }
        public string Mode { get; set; }
        public string Hint { get; set; }
        public string ExpectedNextStep { get; set; }
    }

    public class LearningHintResponse
    {
        public string Hint { get; set; }
        public string ExpectedNextStep { get; set; }
    }

    public class LearningSwitchModeResponse
    {
        public string Message { get; set; }
        public string CurrentMode { get; set; }
    }

    public class LearningDescriptionResponse
    {
        public string Description { get; set; }
        public string ExpectedExpression { get; set; }
    }

    public class LearningStatusResponse
    {
        public bool RequiresInput { get; set; }
        public bool IsCompleted { get; set; }
        public string CurrentState { get; set; }
        public string Hint { get; set; }
        public string ExpectedNextStep { get; set; }
    }
}
