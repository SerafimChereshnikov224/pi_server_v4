using System.Collections.Concurrent;
using System.Threading.Tasks;
using PiServer.Services;

namespace PiServer.Models
{
    public class Channel
    {
        public string Name { get; }
        private readonly ConcurrentQueue<string> _messages = new();
        private readonly SemaphoreSlim _messageAvailable = new(0);

        public Channel(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        

        public List<string> DequeueAll(string? filter = null)
        {
            var result = new List<string>();

            int availableCount = _messageAvailable.CurrentCount;
            for (int i = 0; i < availableCount; i++)
            {
                if (_messages.TryDequeue(out var msg))
                {
                    _messageAvailable.Wait(); // уменьшаем семафор

                    if (string.IsNullOrEmpty(filter) || msg.Contains(filter))
                    {
                        result.Add(msg);
                    }
                    else
                    {
                        // вернуть обратно, увеличив семафор
                        _messages.Enqueue(msg);
                        _messageAvailable.Release();
                    }
                }
            }

            return result;
        }

        public Task SendAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty");

            _messages.Enqueue(message);
            _messageAvailable.Release();
            Console.WriteLine($"[DEBUG] Message sent: {message}, semaphore count: {_messageAvailable.CurrentCount}");

            return Task.CompletedTask;
        }


        public async Task<string?> ReceiveAsync(CancellationToken ct = default)
        {
            try
            {
                await _messageAvailable.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            _messages.TryDequeue(out var message);
            return message;
        }



        public EnvironmentManager? Environment { get; set; }

        public bool TryDeliver(string message)
        {
            if (message == null)
                return false;

            _ = SendAsync(message);
            return true;
        }

        public bool TryRegisterReceiver(Func<string, Task> receiver)
        {
            // Упрощенная реализация (без реальной подписки)
            // В будущем можно добавить список подписчиков
            return false;
        }
    }
}