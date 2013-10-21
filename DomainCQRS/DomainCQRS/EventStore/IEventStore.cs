using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Interace for an event store.
	/// </summary>
	public interface IEventStore
	{
		ILogger Logger { get; }
		IEventPersister EventStoreProvider { get; }
		IEventSerializer EventSerializer { get; }

		// Messages
		/// <summary>
		/// Save an event to the provider.
		/// </summary>
		/// <param name="aggregateRootId">The Aggregate Root Id to store the event against.</param>
		/// <param name="version">The version of the Aggregate Root, this is used for optimistic concurrency.</param>
		/// <param name="aggregateRootType">The type of the Aggregate Root, can be used when reconstructing the Aggregate Root.</param>
		/// <param name="event">The event to store.</param>
		/// <returns>The event store.</returns>
		IEventStore Save(Guid aggregateRootId, int version, Type aggregateRootType, object @event);
		/// <summary>
		/// Loads events from the persister.
		/// </summary>
		/// <param name="aggregateRootId">The Aggregate Root Id to load events for.</param>
		/// <param name="fromVersion">The minimum version to load from. Null means no minimum.</param>
		/// <param name="toVersion">The maximum version to load to. Null means no maximum.</param>
		/// <param name="fromTimestamp">The date/time to load events from.  Null means from the beginning.</param>
		/// <param name="toTimestamp">The date/time to load events to.  Null means to the end.</param>
		/// <returns>The stored events.</returns>
		IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp);
		/// <summary>
		/// Fired when an event is stored.
		/// </summary>
		event EventHandler<StoredEvent> EventStored;

		// Publishing
		/// <summary>
		/// Creates a marker that can be used by the persister to load any new events from the last marker.
		/// The marker can be persisted to be used for each subsequent call.
		/// </summary>
		/// <returns>A new marker.</returns>
		IEventPersisterPosition CreateEventStoreProviderPosition();
		/// <summary>
		/// Returns any new events since the from position.
		/// </summary>
		/// <param name="batchSize">The maximum number of events that should be returned.</param>
		/// <param name="from">The marker from which events should be returned. If a newly created one is passed in then events are returned from the beginning.</param>
		/// <param name="to">A newly created position marker that is set to the last event that was returned, it should be used for the next call as the <paramref name="from"/> parameter.</param>
		/// <returns>The events loaded.</returns>
		IEnumerable<StoredEvent> Load(int batchSize, IEventPersisterPosition from, out IEventPersisterPosition to);

		// Event Upgrading
		/// <summary>
		/// Registers an event to be upgraded as it loaded from the event store provider.
		/// The event that needs to be upgraded will be passed as the only argument into the constructor of the event it will be upgraded to.
		/// </summary>
		/// <typeparam name="Event">The event type to be upgraded.</typeparam>
		/// <typeparam name="UpgradedEvent">The event type it will be upgraded to. <typeparamref name="UpradedEvent"/> should have a constructor that only takes <typeparamref name="Event"/> as a parameter.</typeparam>
		/// <returns>The event store.</returns>
		IEventStore Upgrade<Event, UpgradedEvent>();
	}
}
