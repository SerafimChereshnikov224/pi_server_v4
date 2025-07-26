using PiServer.Models;

namespace PiServer.Services
{
    
    public interface ISessionProcessStorage
    {
        ProcessBuilder GetOrCreateBuilder(string sessionId, EnvironmentManager environment);
        void Reset(string sessionId);
    }

}
