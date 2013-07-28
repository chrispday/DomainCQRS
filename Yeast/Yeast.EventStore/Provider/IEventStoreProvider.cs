using System;
using System.Collections.Generic;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventStoreProvider : IDisposable
	{
		ILogger Logger { get; set; }
		IEventStoreProvider EnsureExists();
		IEventStoreProvider Save(EventToStore eventToStore);
		IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);

		IEventStoreProviderPosition CreateEventStoreProviderPosition();
		IEnumerable<EventToStore> Load(IEventStoreProviderPosition to);
		IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to);
	}
}
