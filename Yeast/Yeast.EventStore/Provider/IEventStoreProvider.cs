using System;
using System.Collections.Generic;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventStoreProvider : IDisposable
	{
		ILogger Logger { get; set; }
		IEventStoreProvider EnsureExists();

		// Events
		IEventStoreProvider Save(EventToStore eventToStore);
		IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);

		// Position
		IEventStoreProviderPosition CreatePosition();
		IEventStoreProviderPosition LoadPosition(Guid subscriberId);
		IEventStoreProvider SavePosition(Guid subscriberId, IEventStoreProviderPosition position);
		IEnumerable<EventToStore> Load(IEventStoreProviderPosition from, IEventStoreProviderPosition to);
	}
}
