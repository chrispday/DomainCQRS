using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	public interface IPartitionedEventPersister : IEventPersister
	{
		int MaximumPartitions { get; }
	}
}
