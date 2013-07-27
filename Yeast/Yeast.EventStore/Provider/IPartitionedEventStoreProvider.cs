using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IPartitionedEventStoreProvider : IEventStoreProvider
	{
		int MaximumPartitions { get; set; }
	}
}
