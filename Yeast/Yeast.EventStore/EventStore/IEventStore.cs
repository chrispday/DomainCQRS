using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventStore
	{
		ILogger Logger { get; set; }
		IEventStoreProvider EventStoreProvider { get; set; }
		IEventSerializer EventSerializer { get; set; }

		// Messages
		IEventStore Save<T>(Guid aggregateRootId, int version, T data);
		IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);

		// Publishing
		IEventStoreProviderPosition CreateEventStoreProviderPosition();
		IEnumerable<StoredEvent> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to);
	}
}
