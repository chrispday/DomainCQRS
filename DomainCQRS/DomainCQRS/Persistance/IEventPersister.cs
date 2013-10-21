using System;
using System.Collections.Generic;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Contract to provide event persistance.
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
		/// Persist an event.
		/// </summary>
		/// <param name="eventToStore">The event and associated meta-data to store.</param>
		/// <returns>The <see cref="IEventPersister"/></returns>
		IEventPersister Save(EventToStore eventToStore);
		/// <summary>
		/// Load events from persistance.
		/// </summary>
		/// <param name="aggregateRootId">The Aggregate Root to load events for.</param>
		/// <param name="aggregateRootId">The Aggregate Root Id to load events for.</param>
		/// <param name="fromVersion">The minimum version to load from. Null means no minimum.</param>
		/// <param name="toVersion">The maximum version to load to. Null means no maximum.</param>
		/// <param name="fromTimestamp">The date/time to load events from.  Null means from the beginning.</param>
		/// <param name="toTimestamp">The date/time to load events to.  Null means to the end.</param>
		/// <returns>The stored events.</returns>
		IEnumerable<EventToStore> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);

		// Position
		/// <summary>
		/// Creates a marker that can be used to load any new events from the last marker.
		/// The marker can be persisted to be used for each subsequent call.
		/// </summary>
		/// <returns>A new marker.</returns>
		IEventPersisterPosition CreatePosition();
		/// <summary>
		/// Loads the saved position for a subscriber.
		/// </summary>
		/// <param name="subscriberId">The subscriber id.</param>
		/// <returns>The saved position, or a new position if not previously saved.</returns>
		IEventPersisterPosition LoadPosition(Guid subscriberId);
		/// <summary>
		/// Saves the position for a subscriber.
		/// </summary>
		/// <param name="subscriberId">The subscriber id.</param>
		/// <param name="position">The <see cref="IEventPersisterPosition"/> to save.</param>
		/// <returns>The <see cref="IEventPersister"/></returns>
		IEventPersister SavePosition(Guid subscriberId, IEventPersisterPosition position);
		/// <summary>
		/// Returns any new events since the from position.
		/// </summary>
		/// <param name="from">The marker from which events should be returned. If a newly created one is passed in then events are returned from the beginning.</param>
		/// <param name="to">A p[osition marker that will be set to the last event that was returned, it should be used for the next call as the <paramref name="from"/> parameter.</param>
		/// <returns>The events loaded.</returns>
		IEnumerable<EventToStore> Load(IEventPersisterPosition from, IEventPersisterPosition to);
	}
}
