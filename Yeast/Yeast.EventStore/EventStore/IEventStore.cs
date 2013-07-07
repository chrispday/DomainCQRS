using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IEventStore
	{
		IEventStoreProvider EventStoreProvider { get; set; }

		IEventStore Save<T>(Guid aggregateId, int version, T data);
		IEnumerable<StoredEvent> Load(Guid aggregateId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);
	}
}
