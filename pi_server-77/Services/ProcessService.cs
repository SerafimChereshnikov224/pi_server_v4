using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PiServer.Models;

namespace PiServer.Services
{
    public class ProcessService
    {
        private readonly EnvironmentManager _environment;
        private ProcessBuilder _builder;

        public ProcessService(EnvironmentManager environment)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _builder = new ProcessBuilder(_environment);
        }

        public async Task AddReplicationAsync(Func<IProcess> processFactory)
        {
            _builder.AddReplication(processFactory);
            await Task.CompletedTask;
        }

        public async Task AddNewChannelAsync(string name, ChannelStrategy strategy)
        {
            _builder.AddNewChannel(name, strategy);
            await Task.CompletedTask;
        }
        public async Task<ProcessResponse> AddSendAsync(SendRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                var channelName = request.Channel ?? "default_channel";
                var channel = _environment.GetOrCreateChannel(channelName);

                IProcess? continuation = null;
                if (request.Continuation != null)
                {
                    var continuationBuilder = new ProcessBuilder(_environment);
                    await BuildProcessFromRequest(continuationBuilder, request.Continuation);
                    continuation = continuationBuilder.GetCurrentProcess();
                }

                _builder.AddSend(channelName, request.Message ?? string.Empty, continuation);

                await _environment.TransferAsync(channelName, request.Message ?? string.Empty);

                return SuccessResponse(result: _builder.GetProcessDiagram());
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public async Task<ProcessResponse> AddReceiveAsync(ReceiveRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                var channelName = request.Channel ?? "default_channel";
                var channel = _environment.GetOrCreateChannel(channelName);

                IProcess? continuation = null;
                if (request.Continuation != null)
                {
                    var continuationBuilder = new ProcessBuilder(_environment);
                    await BuildProcessFromRequest(continuationBuilder, request.Continuation);
                    continuation = continuationBuilder.GetCurrentProcess();
                }

                if (!request.WaitForMessage)
                {
                    var messages = channel.DequeueAll(request.Filter);

                    _builder.AddReceive(
                        channel: channelName,
                        filter: request.Filter ?? string.Empty,
                        handler: async msg =>
                        {
                            if (continuation != null)
                                await continuation.ExecuteAsync(_environment);
                        });

                    return SuccessResponse(result: string.Join("; ", messages), diagram: _builder.GetProcessDiagram());
                }

                _builder.AddReceive(
                    channel: channelName,
                    filter: request.Filter ?? string.Empty,
                    continuation: msg =>
                    {
                        if (continuation != null)
                            _ = continuation.ExecuteAsync(_environment);
                        return continuation;
                    });

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string? receivedMessage = await channel.ReceiveAsync(cts.Token);

                if (receivedMessage != null &&
                    request.Filter != null &&
                    !receivedMessage.Contains(request.Filter))
                {
                    receivedMessage = null;
                }

                return SuccessResponse(result: receivedMessage, diagram: _builder.GetProcessDiagram());
            }
            catch (OperationCanceledException)
            {
                return ErrorResponse("Receive timeout");
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public async Task<ProcessResponse> AddParallelAsync(ParallelRequest request)
        {
            var results = new List<object>();

            foreach (var process in request.Processes)
            {
                var json = process.Data.GetRawText();

                switch (process.Type?.ToLowerInvariant())
                {
                    case "send":
                        var send = System.Text.Json.JsonSerializer.Deserialize<SendRequest>(json);
                        var sendResponse = await AddSendAsync(send);
                        results.Add(new
                        {
                            processId = Guid.NewGuid(),
                            type = "send",
                            channel = send.Channel,
                            message = sendResponse?.Result ?? "N/A",
                            status = sendResponse?.Success == true ? "sent" : "failed",
                            timestamp = DateTime.UtcNow
                        });
                        break;

                    case "receive":
                        var receive = System.Text.Json.JsonSerializer.Deserialize<ReceiveRequest>(json);
                        var receiveResponse = await AddReceiveAsync(receive);
                        results.Add(new
                        {
                            processId = Guid.NewGuid(),
                            type = "receive",
                            channel = receive.Channel,
                            message = receiveResponse?.Result ?? "N/A",
                            status = receiveResponse?.Success == true ? "received" : "timeout",
                            timestamp = DateTime.UtcNow
                        });
                        break;

                    case "parallel":
                        var parallel = System.Text.Json.JsonSerializer.Deserialize<ParallelRequest>(json);
                        var parallelResponse = await AddParallelAsync(parallel);
                        if (parallelResponse?.Results != null)
                        {
                            results.AddRange(parallelResponse.Results);
                        }
                        break;

                    default:
                        return ErrorResponse($"Unknown process type: {process.Type}");
                }
            }

            return SuccessResponse(results: results);
        }

        public async Task<ProcessResponse> AddReplicationAsync(ReplicationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                var templateBuilder = new ProcessBuilder(_environment);
                await BuildProcessFromRequest(templateBuilder, request.ProcessTemplate);

                var templateProcess = templateBuilder.GetCurrentProcess();
                _builder.AddReplication(() => templateProcess);

                return SuccessResponse(result: _builder.GetProcessDiagram());
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public async Task<ProcessResponse> AddNewChannelAsync(NewChannelRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                _builder.AddNewChannel(
                    name: request.Name ?? "default_channel",
                    strategy: request.Strategy
                );
                return SuccessResponse(result: _builder.GetProcessDiagram());
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public async Task<ProcessResponse> ExecuteAsync()
        {
            try
            {
                await _builder.ExecuteAsync();
                return SuccessResponse(result: "Execution completed");
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        public ProcessResponse Reset()
        {
            _builder = new ProcessBuilder(_environment);
            return SuccessResponse(result: "Process reset");
        }

        public ProcessResponse GetCurrentDiagram()
        {
            return SuccessResponse(result: "Current diagram");
        }

        private async Task BuildProcessFromRequest(ProcessBuilder builder, ProcessRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            switch (request.Type?.ToLower())
            {
                case "send":
                    var sendData = JsonConvert.DeserializeObject<SendRequest>(request.Data.ToString() ?? "{}");
                    if (sendData != null) await AddSendAsync(sendData);
                    break;

                case "receive":
                    var receiveData = JsonConvert.DeserializeObject<ReceiveRequest>(request.Data.ToString() ?? "{}");
                    if (receiveData != null) await AddReceiveAsync(receiveData);
                    break;

                case "parallel":
                    var parallelData = JsonConvert.DeserializeObject<ParallelRequest>(request.Data.ToString() ?? "{}");
                    if (parallelData != null) await AddParallelAsync(parallelData);
                    break;

                case "replication":
                    var replicationData = JsonConvert.DeserializeObject<ReplicationRequest>(request.Data.ToString() ?? "{}");
                    if (replicationData != null) await AddReplicationAsync(replicationData);
                    break;

                case "newchannel":
                    var channelData = JsonConvert.DeserializeObject<NewChannelRequest>(request.Data.ToString() ?? "{}");
                    if (channelData != null) await AddNewChannelAsync(channelData);
                    break;

                default:
                    builder.AddInactive();
                    break;
            }
        }

        private ProcessResponse SuccessResponse(string? diagram = null, string? result = null, List<object>? results = null)
        {
            return new ProcessResponse
            {
                Diagram = diagram ?? _builder.GetProcessDiagram(),
                Result = result ?? "",
                Results = results ?? new List<object>(),
                Success = true,
                Error = ""
            };
        }

        private ProcessResponse ErrorResponse(string error, string? diagram = null)
        {
            return new ProcessResponse
            {
                Diagram = diagram ?? _builder.GetProcessDiagram(),
                Result = "",
                Results = new List<object>(),
                Success = false,
                Error = error ?? "Unknown error"
            };
        }
    }
}
