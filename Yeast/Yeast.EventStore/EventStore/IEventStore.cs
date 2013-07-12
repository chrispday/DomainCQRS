using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IEventStore
	{
		IEventStoreProvider EventStoreProvider { get; set; }

		IEventStore Save<T>(Guid aggregateRootId, int version, T data);
		IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);
	}
}
