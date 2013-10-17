using System;
using System.Collections.Generic;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Contract to provide event storage.
	/// <see cref="IDisposable"/> is required to clean-up any connections to a persistance medium.
	/// </summary>
	public interface IEventPersister : IDisposable
	{
		ILogger Logger { get; }
		/// <summary>
		/// Called to ensure the underlying persistance is available, can be called multiple times.
		/// </summary>
		/// <returns><see cref="IEventPersister"/></returns>
		IEventPersister EnsureExists();

		// Events
		/// <summary>
		/// Save an event.
		/// </summary>
		/// <param name="eventToStore"></param>
		/// <returns></returns>
		IEventPersister Save(EventToStore eventToStore);
		IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);

		// Position
		IEventPersisterPosition CreatePosition();
		IEventPersisterPosition LoadPosition(Guid subscriberId);
		IEventPersister SavePosition(Guid subscriberId, IEventPersisterPosition position);
		IEnumerable<EventToStore> Load(IEventPersisterPosition from, IEventPersisterPosition to);
	}
}
