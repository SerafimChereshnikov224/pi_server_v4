using System.Threading.Tasks;
using PiServer.Services;

namespace PiServer.Models
{
    public class ReplicationProcess : IProcess
    {
        private readonly Func<IProcess> _processFactory;

        public ReplicationProcess(Func<IProcess> processFactory)
        {
            _processFactory = processFactory;
        }

        public async Task ExecuteAsync(EnvironmentManager environment)
        {
            while (true)
            {
                var processInstance = _processFactory();
                _ = Task.Run(() => processInstance.ExecuteAsync(environment));
                await Task.Delay(10);
            }
        }
    }
}