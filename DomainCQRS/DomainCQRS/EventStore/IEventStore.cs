using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public interface IEventStore
	{
		ILogger Logger { get; }
		IEventStoreProvider EventStoreProvider { get; }
		IEventSerializer EventSerializer { get; }

		// Messages
		IEventStore Save(Guid aggregateRootId, int version, Type aggregateRootType, object data);
		IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);
		event EventHandler<StoredEvent> EventStored;

		// Publishing
		IEventStoreProviderPosition CreateEventStoreProviderPosition();
		IEnumerable<StoredEvent> Load(int batchSize, IEventStoreProviderPosition from, IEventStoreProviderPosition to);

		// Event Upgrading
		IEventStore Upgrade<Event, UpgradedEvent>();
	}
}
