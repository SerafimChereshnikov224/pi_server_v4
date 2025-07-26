using System.Threading.Tasks;
using PiServer.Services;

namespace PiServer.Models
{
    public class InactiveProcess : IProcess
    {
        public async Task ExecuteAsync(EnvironmentManager environment)
        {
            await Task.CompletedTask;
        }
    }
}