using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PiServer.Services;

namespace PiServer.Models
{
    public class ParallelProcess : IProcess
    {
        private readonly List<IProcess> _processes;

        public ParallelProcess(params IProcess[] processes)
        {
            _processes = processes.ToList();
        }

        public ParallelProcess(IEnumerable<IProcess> processes)
        {
            _processes = processes.ToList();
        }

        public async Task ExecuteAsync(EnvironmentManager environment)
        {
            environment.LogMessage($"[{DateTime.Now:HH:mm:ss.fff}] PARALLEL START");
            var tasks = _processes
                .Where(p => p != null)
                .Select(p => p.ExecuteAsync(environment))
                .ToList();

            await Task.WhenAll(tasks);
            environment.LogMessage($"[{DateTime.Now:HH:mm:ss.fff}] PARALLEL END");

        }

        // Добавляем удобные методы для работы
        public ParallelProcess AddProcess(IProcess process)
        {
            if (process != null)
                _processes.Add(process);
            return this;
        }

        public static ParallelProcess operator +(ParallelProcess parallel, IProcess process)
            => parallel.AddProcess(process);

        public IReadOnlyList<IProcess> GetProcesses() => _processes.AsReadOnly();
    }
}