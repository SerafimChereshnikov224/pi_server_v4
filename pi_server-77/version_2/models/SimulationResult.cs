using System.Collections.Generic;

namespace PiServer.version_2.models
{
	public class SimulationResult
	{
		public bool IsDeadlocked { get; set; }
		public int StepsExecuted { get; set; }
		public string FinalState { get; set; } = string.Empty;
		public List<string> DeadlockedProcesses { get; set; } = new();
	}
}