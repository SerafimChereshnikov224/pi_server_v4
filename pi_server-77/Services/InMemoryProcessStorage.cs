using System.Collections.Concurrent;
using PiServer.Models;
using PiServer.Services;

namespace PiServer.Storage
{

    public class InMemoryProcessStorage : ISessionProcessStorage
    {
        private readonly ConcurrentDictionary<string, ProcessBuilder> _storage = new();
        private readonly EnvironmentManager _environment;

        public InMemoryProcessStorage(EnvironmentManager environment)
        {
            _environment = environment;
        }


        private readonly Dictionary<string, ProcessBuilder> _builders = new();


        public ProcessBuilder GetOrCreateBuilder(string sessionId, EnvironmentManager environment)
        {
            if (!_builders.TryGetValue(sessionId, out var builder))
            {
                builder = new ProcessBuilder(environment);
                _builders[sessionId] = builder;
            }
            return builder;
        }


        public void Reset(string sessionId)
        {
            _storage[sessionId] = new ProcessBuilder(_environment);
        }
    }
}
