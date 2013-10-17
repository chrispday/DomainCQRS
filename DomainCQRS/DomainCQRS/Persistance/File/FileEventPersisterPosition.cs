using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Persister
{
	[Serializable]
	public class FileEventPersisterPosition : IEventPersisterPosition
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
	public class PartitionedFileEventPersisterPosition : IEventPersisterPosition
	{
		public IEventPersisterPosition[] Positions;

		public PartitionedFileEventPersisterPosition(int maximumPartitions)
		{
			Positions = new IEventPersisterPosition[maximumPartitions];
			for (int i = 0; i < maximumPartitions; i++)
			{
				Positions[i] = new FileEventPersisterPosition();
			}
		}
	}
}
