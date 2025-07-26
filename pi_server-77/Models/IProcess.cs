using PiServer.Services;


namespace PiServer.Models
{
    public interface IProcess
    {
        Task ExecuteAsync(EnvironmentManager environment);
    }
}