using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PiServer.version_2.runtime
{
    public class Channel
    {
        public string Name { get; }

        private readonly Queue<object> _messages = new();
        private readonly Queue<TaskCompletionSource<object>> _waitingReceivers = new();

        public Channel(string name)
        {
            Name = name;
        }
        public async Task SendAsync(object message, bool broadcast = false)
        {
            if (broadcast)
            {
                while (_waitingReceivers.TryDequeue(out var receiver))
                {
                    receiver.SetResult(message); // каждому своя копия
                }
                // Не сохраняем в очередь
            }
            else
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
        }
        public async Task<object> ReceiveAsync()
        {
            if (_messages.TryDequeue(out var msg))
            {
                return msg;
            }

            var tcs = new TaskCompletionSource<object>();
            _waitingReceivers.Enqueue(tcs);
            return await tcs.Task;
        }

        public bool HasMessages()
        {
            return _messages.Count > 0;
        }

        public IEnumerable<object> GetMessages()
        {
            return _messages.ToArray();
        }
    }
}
