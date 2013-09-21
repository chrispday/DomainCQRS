using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	public interface IPartitionedEventStoreProvider : IEventStoreProvider
	{
		int MaximumPartitions { get; set; }
	}
}
