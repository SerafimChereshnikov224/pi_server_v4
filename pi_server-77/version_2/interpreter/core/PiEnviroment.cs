using PiServer.version_2.runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PiServer.version_2.interpreter.core
{
    public class PiEnvironment : IDisposable
    {
        private readonly Dictionary<string, Channel> _channels = new();
        private readonly HashSet<string> _restrictedNames = new();
        private readonly Dictionary<string, object?> _variables = new();

        public IReadOnlyDictionary<string, object?> Variables => _variables;
        public IReadOnlyDictionary<string, Channel> Channels => _channels;
        public IReadOnlyCollection<string> ActiveRestrictions => _restrictedNames.ToList().AsReadOnly();


        public object? GetVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (_variables.TryGetValue(name, out var val))
                return val;

            // если нет — возвращаем null, а не само имя
            return null;
        }

        public void SetVariable(string name, object? value)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            _variables[name] = value;
            Console.WriteLine($"[PiEnv] Set {name} = {value}");
        }

public Channel GetChannel(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Channel name cannot be empty", nameof(name));

    // Нормализуем имя канала
    var key = name.Trim().ToLowerInvariant();

    if (_restrictedNames.Contains(key))
        throw new Exception($"Channel {key} is restricted");

    if (!_channels.TryGetValue(key, out var channel))
    {
        channel = new Channel(key); // ✅ передаём имя в конструктор
        _channels[key] = channel;
        Console.WriteLine($"[PiEnv] Created channel '{key}'");
    }

    return channel;
}




        public async Task SendAsync(string channelName, object message, bool broadcast = false)
        {
            var channel = GetChannel(channelName);
            await channel.SendAsync(message, broadcast);
            Console.WriteLine($"[PiEnv] Sent '{message}' via '{channelName}' (broadcast={broadcast})");
        }

        public async Task<object> ReceiveAsync(string channelName)
        {
            var channel = GetChannel(channelName);
            var msg = await channel.ReceiveAsync();
            Console.WriteLine($"[PiEnv] Received '{msg}' from '{channelName}'");
            return msg;
        }

        public IReadOnlyCollection<string> GetChannelState(string channelName)
        {
            if (_channels.TryGetValue(channelName, out var channel))
                return channel.GetMessages()
                    .Select(m => m?.ToString() ?? "null")
                    .ToList()
                    .AsReadOnly();

            return new List<string>().AsReadOnly();
        }

        public IDisposable Restrict(string name)
        {
            _restrictedNames.Add(name);
            return new Disposable(() => _restrictedNames.Remove(name));
        }

        public void Dispose()
        {
            _channels.Clear();
            _variables.Clear();
            _restrictedNames.Clear();
        }

        private class Disposable : IDisposable
        {
            private readonly Action _action;
            public Disposable(Action action) => _action = action;
            public void Dispose() => _action();
        }
    }


    
}
