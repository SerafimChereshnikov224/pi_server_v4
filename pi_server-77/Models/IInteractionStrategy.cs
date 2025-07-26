namespace PiServer.Models
{
    public interface IInteractionStrategy
    {
        void Send(Channel channel, string message);
        void Receive(Channel channel, Action<string> callback);
    }
}
