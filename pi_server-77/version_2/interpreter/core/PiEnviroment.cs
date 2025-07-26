namespace PiServer.version_2.interpreter.core
{
    public class PiEnvironment : IDisposable
    {
        private readonly Dictionary<string, Channel> _channels = new();
        private readonly HashSet<string> _restrictedNames = new();

        public Channel GetChannel(string name)
        {
            if (_restrictedNames.Contains(name))
                throw new Exception($"Channel {name} is restricted");

            if (!_channels.TryGetValue(name, out var channel))
            {
                channel = new Channel();
                _channels[name] = channel;
            }
            return channel;
        }

        public async Task SendAsync(string channelName, string message)
        {
            var channel = GetChannel(channelName);
            await channel.SendAsync(message);
        }

        public async Task<string> ReceiveAsync(string channelName)
        {
            var channel = GetChannel(channelName);
            return await channel.ReceiveAsync();
        }

        public IDisposable Restrict(string name)
        {
            _restrictedNames.Add(name);
            return new Disposable(() => _restrictedNames.Remove(name));
        }

        public void Dispose() => _channels.Clear();

        private class Disposable : IDisposable
        {
            private readonly Action _action;
            public Disposable(Action action) => _action = action;
            public void Dispose() => _action();
        }
    }

    public class Channel
    {
        private readonly Queue<string> _messages = new();
        private readonly Queue<TaskCompletionSource<string>> _waitingReceivers = new();

        public async Task SendAsync(string message)
        {
            if (_waitingReceivers.TryDequeue(out var receiver))
            {
                receiver.SetResult(message);
            }
            else
            {
                _messages.Enqueue(message);
            }
        }

        public async Task<string> ReceiveAsync()
        {
            if (_messages.TryDequeue(out var message))
            {
                return message;
            }

            var tcs = new TaskCompletionSource<string>();
            _waitingReceivers.Enqueue(tcs);
            return await tcs.Task;
        }
    }
}
