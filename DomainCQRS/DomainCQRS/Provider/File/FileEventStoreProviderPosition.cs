using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Provider
{
	[Serializable]
	public class FileEventStoreProviderPosition : IEventStoreProviderPosition
	{
		public Dictionary<Guid, long> Positions = new Dictionary<Guid,long>();

		public override string ToString()
		{
			if (0 == Positions.Count)
			{
				return "<Empty>";
			}

			var sb = new StringBuilder();
			foreach (var p in Positions)
			{
				sb.AppendFormat("{0} -> {1}", p.Key, p.Value).AppendLine();
			}
			return sb.ToString();
		}
	}

	[Serializable]
	public class PartitionedFileEventStoreProviderPosition : IEventStoreProviderPosition
	{
		public IEventStoreProviderPosition[] Positions;

		public PartitionedFileEventStoreProviderPosition(int maximumPartitions)
		{
			Positions = new IEventStoreProviderPosition[maximumPartitions];
			for (int i = 0; i < maximumPartitions; i++)
			{
				Positions[i] = new FileEventStoreProviderPosition();
			}
		}
	}
}
