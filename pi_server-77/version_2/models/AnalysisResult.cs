using System.Collections.Generic;
using System.Linq;

namespace PiServer.version_2.models
{
	public class AnalysisResult
	{
		// Все имена каналов, которые встречаются в процессе (включая определённые и используемые)
		public HashSet<string> AllChannelNames { get; set; } = new();

		// Каналы, определённые через restriction ({*x} ...)
		public HashSet<string> DefinedChannels { get; set; } = new();

		// Каналы, которые используются хотя бы в одном входе или выходе
		public HashSet<string> UsedChannels { get; set; } = new();

		// Каналы, которые используются только как вход (но не как выход)
		public HashSet<string> InputOnlyChannels { get; set; } = new();

		// Каналы, которые используются только как выход (но не как вход)
		public HashSet<string> OutputOnlyChannels { get; set; } = new();

		// Каналы, определённые, но не используемые
		public HashSet<string> UnusedDefinedChannels =>
			new HashSet<string>(DefinedChannels.Except(UsedChannels));

		// Неиспользуемые подпроцессы (например, NullProcess или ветки без действий)
		public List<string> DeadProcessDescriptions { get; set; } = new();

		// Флаг, есть ли потенциальные проблемы (deadlock, потеря сообщений)
		public bool HasIssues => InputOnlyChannels.Count > 0 || OutputOnlyChannels.Count > 0 || UnusedDefinedChannels.Count > 0;

		public override string ToString()
		{
			return $"Used channels: {string.Join(", ", UsedChannels)}\n" +
				   $"Defined channels: {string.Join(", ", DefinedChannels)}\n" +
				   $"Unused defined channels: {string.Join(", ", UnusedDefinedChannels)}\n" +
				   $"Input-only channels: {string.Join(", ", InputOnlyChannels)}\n" +
				   $"Output-only channels: {string.Join(", ", OutputOnlyChannels)}\n" +
				   $"Dead processes: {string.Join("; ", DeadProcessDescriptions)}";
		}
	}
}