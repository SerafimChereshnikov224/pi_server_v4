using PiServer.Services;
using PiServer.Models;
using System.Threading.Tasks;

namespace PiServer.Models
{
    public class ChannelDrivenStrategy : IInteractionStrategy
    {
        public void Send(Channel channel, string message)
        {
            channel.TryDeliver(message);
        }

        public void Receive(Channel channel, Action<string> callback)
        {
            // Convert the Action<string> to Func<string, Task>
            Func<string, Task> asyncCallback = async (msg) => 
            {
                callback(msg);
                await Task.CompletedTask;
            };
            
            channel.TryRegisterReceiver(asyncCallback);
        }
    }
}