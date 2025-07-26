namespace PiServer.Models
{
    public class EnvDrivenStrategy : IInteractionStrategy
    {
        public void Send(Channel channel, string message)
        {
            if (channel.Environment != null)
            {
                channel.Environment.RegisterSend(channel, message);
            }
        }

        public void Receive(Channel channel, Action<string> callback)
        {
            if (channel.Environment != null)
            {

                channel.Environment.RegisterReceive(channel, value =>
                {
                    callback(value); // вызываем оригинальный Action
                    return Task.CompletedTask; // возвращаем Task, т.к. требуется async
                });
            }
        }

    }
}
