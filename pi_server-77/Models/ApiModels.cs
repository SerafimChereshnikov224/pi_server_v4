// Models/ApiModels.cs
using System;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;


using PiServer.Services;

namespace PiServer.Models
{
    public class SendRequest
    {
        public string? Channel { get; set; }
        public string? Message { get; set; }
        public ProcessRequest? Continuation { get; set; }
    }

    public class ReceiveRequest
    {
        public string? Channel { get; set; }
        public string? Filter { get; set; }
        public ProcessRequest? Continuation { get; set; }
        public bool WaitForMessage { get; set; } = true;
    }

    public class ParallelRequest
    {
        public List<ProcessRequest> Processes { get; set; } = new();
    }

    public class ReplicationRequest
    {
        public ProcessRequest? ProcessTemplate { get; set; }
    }


    public class NewChannelRequest
    {
        public string Name { get; set; } = "default_channel";

        [JsonConverter(typeof(JsonStringEnumConverter))] // ← Добавьте это
        public ChannelStrategy Strategy { get; set; } = ChannelStrategy.PassiveEnvironment;
    }

    public class ProcessRequest
    {
        public string? Type { get; set; }
        public JsonElement Data { get; set; }
    }



    public class ProcessResponse
    {
        public bool Success { get; set; }
        public string? Result { get; set; }
        public string? Error { get; set; }
        public List<object> Results { get; set; } = new();
        public string? Diagram { get; set; }  // ← Добавь эту строку
    }



}