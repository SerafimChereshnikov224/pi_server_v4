using System.Threading.Tasks;
using PiServer.Services;

namespace PiServer.Models
{
    public class NewChannelProcess : IProcess
    {
        private readonly string _channelName;
        private readonly ChannelStrategy _strategy;

        public NewChannelProcess(string channelName, ChannelStrategy strategy)
        {
            _channelName = channelName;
            _strategy = strategy;
        }

        public async Task ExecuteAsync(EnvironmentManager environment)
        {
            environment.GetOrCreateChannel(_channelName, _strategy);
            await Task.CompletedTask;
        }
    }
}
