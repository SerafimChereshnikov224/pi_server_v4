namespace PiServer.version_2.runtime
{
    public class Channel
    {
        public string Name { get; }
        private readonly Queue<string> _messages = new();

        public Channel(string name) => Name = name;

        public void Send(string message)
        {
            _messages.Enqueue(message);
        }

        public bool TryReceive(out string message)
        {
            return _messages.TryDequeue(out message);
        }
    }
}
