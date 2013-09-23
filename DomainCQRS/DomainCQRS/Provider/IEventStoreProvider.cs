using System;
using System.Collections.Generic;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public interface IEventStoreProvider : IDisposable
	{
		ILogger Logger { get; }
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
