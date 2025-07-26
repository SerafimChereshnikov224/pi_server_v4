namespace PiServer.Models
{
    public class MixedInteractionStrategy : IInteractionStrategy
    {
        public void Send(Channel channel, string message)
        {
            // If no receiver is needed for sending, pass a dummy async lambda
            if (channel.Environment != null)
            {

                channel.Environment.MixInteraction(channel, message, _ => Task.CompletedTask);
            }
        }

        public void Receive(Channel channel, Action<string> callback)
        {
            // Convert Action<string> to Func<string, Task>
            Func<string, Task> asyncCallback = msg => 
            {
                callback(msg);
                return Task.CompletedTask;
            };

            if (channel?.Environment == null)
                return;
            
            channel.Environment.RegisterReceive(channel, asyncCallback);
        }
    }
}