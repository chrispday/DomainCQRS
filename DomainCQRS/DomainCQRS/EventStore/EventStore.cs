using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	/// <summary>
	/// Configures Domain CQRS to use <see cref="EventStore"/>
	/// </summary>
	public static class EventStoreConfigure
	{
		/// <summary>
		/// The default for the initial buffer size used to serialize events.
		/// </summary>
		public static int DefaultSerializationBufferSize = 1024;
		/// <summary>
		/// Configures Domain CQRS to use <see cref="EventStore"/> using the default serializaion buffer size.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/>.</param>
		/// <returns>The <see cref="IConfigure"/>.</returns>
		public static IConfigure EventStore(this IConfigure configure) { return configure.EventStore(DefaultSerializationBufferSize); }
		/// <summary>
		/// Configures Domain CQRS to use <see cref="EventStore"/>.
		/// A logger, event store provider and serializer should also be configured.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/>.</param>
		/// <param name="defaultSerializationBufferSize">The initial size of the buffer to use when serializing events.</param>
		/// <returns>The <see cref="IConfigure"/>.</returns>
		public static IConfigure EventStore(this IConfigure configure, int defaultSerializationBufferSize)
		{
			configure.Registry
				.BuildInstancesOf<IEventStore>()
				.TheDefaultIs(Registry.Instance<IEventStore>()
					.UsingConcreteType<EventStore>()
					.WithProperty("defaultSerializationBufferSize").EqualTo(defaultSerializationBufferSize))
				.AsSingletons();
			return configure;
		}

		/// <summary>
		/// Configures Domain CQRS to upgrade events as they are loaded.
		/// The old event is passed as the only parameter into a constructor on the event it is to be upgraded to.
		/// </summary>
		/// <typeparam name="Event">The original event.</typeparam>
		/// <typeparam name="UpgradedEvent">The event it should be upgraded to.</typeparam>
		/// <param name="configure">The <see cref="IBuiltConfigure"/>.</param>
		/// <returns>The <see cref="IBuiltConfigure"/>.</returns>
		public static IBuiltConfigure Upgrade<Event, UpgradedEvent>(this IBuiltConfigure configure)
		{
			configure.EventStore.Upgrade<Event, UpgradedEvent>();
			return configure;
		}
	}

	/// <summary>
	/// A delegate that upgrades events.
	/// </summary>
	/// <param name="event">The original event.</param>
	/// <returns>The upgraded event.</returns>
	public delegate object EventUpgrader(object @event);

	/// <summary>
	/// Provides a generic repository for events.
	/// It persists events using a provider.
	/// </summary>
	public class EventStore : IEventStore
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly IEventPersister _eventStoreProvider;
		public IEventPersister EventStoreProvider { get { return _eventStoreProvider; } }
		private readonly IEventSerializer _eventSerializer;
		public IEventSerializer EventSerializer { get { return _eventSerializer; } }
		private readonly int _defaultSerializationBufferSize;
		public int DefaultSerializationBufferSize { get { return _defaultSerializationBufferSize; } }
		private Dictionary<Type, EventUpgrader> _eventUpgraders = new Dictionary<Type, EventUpgrader>();
		protected Extensions.Func<EventToStore, StoredEvent> _eventToStoreFromStoredEvent;

		private static readonly MethodInfo DeserializeMethod = typeof(IEventSerializer).GetMethod("Deserialize");
		private static readonly MethodInfo SerializeMethod = typeof(IEventSerializer).GetMethod("Serialize");

		/// <summary>
		/// Creates an event store.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="eventStoreProvider">The provider.</param>
		/// <param name="eventSerializer">The serializer.</param>
		/// <param name="defaultSerializationBufferSize">The initial size of the buffer for serializing events</param>
		public EventStore(ILogger logger, IEventPersister eventStoreProvider, IEventSerializer eventSerializer, int defaultSerializationBufferSize)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == eventStoreProvider)
			{
				throw new ArgumentNullException("eventStoreProvider");
			}
			if (null == eventSerializer)
			{
				throw new ArgumentNullException("eventSerializer");
			}
			if (0 >= defaultSerializationBufferSize)
			{
				throw new ArgumentOutOfRangeException("defaultSerializationBufferSize");
			}

			_logger = logger;
			_eventStoreProvider = eventStoreProvider;
			_eventSerializer = eventSerializer;
			_defaultSerializationBufferSize = defaultSerializationBufferSize;

			_eventToStoreFromStoredEvent = EventToStoreFromStoredEvent;
		}

		/// <summary>
		/// Fired when an event is stored.
		/// </summary>
		public event EventHandler<StoredEvent> EventStored;

		/// <summary>
		/// Save an event.
		/// </summary>
		/// <param name="aggregateRootId">The Aggregate Root Id to store the event against.</param>
		/// <param name="version">The version of the Aggregate Root, this is used for optimistic concurrency.</param>
		/// <param name="aggregateRootType">The type of the Aggregate Root, can be used when reconstructing the Aggregate Root.</param>
		/// <param name="event">The event to store.</param>
		/// <returns>The event store.</returns>
		public IEventStore Save(Guid aggregateRootId, int version, Type aggregateRootType, object data)
		{
			if (Guid.Empty == aggregateRootId)
			{
				throw new ArgumentOutOfRangeException("aggregateRootId", aggregateRootId, "AggregateRootId cannot be an empty guid.");
			}
			if (1 > version)
			{
				throw new ArgumentOutOfRangeException("version", version, "version cannot be less than 1.");
			}
			if (null == aggregateRootType)
			{
				throw new ArgumentNullException("aggregateRootType");
			}
			if (null == data)
			{
				throw new ArgumentNullException("data");
			}

			var eventToStore = new EventToStore() { AggregateRootId = aggregateRootId, AggregateRootType = aggregateRootType.AssemblyQualifiedName, Version = version, Timestamp = DateTime.Now, EventType = data.GetType().AssemblyQualifiedName, Data = Serialize(data) };
			EventStoreProvider.Save(eventToStore);

			if (null != EventStored)
			{
				EventStored(this, new StoredEvent()
				{
					AggregateRootId = aggregateRootId,
					AggregateRootType = eventToStore.AggregateRootType,
					Event = data,
					Timestamp = eventToStore.Timestamp,
					Version = version
				});
			}

			return this;
		}

		/// <summary>
		/// Loads events from the provider.
		/// </summary>
		/// <param name="aggregateRootId">The Aggregate Root Id to load events for.</param>
		/// <param name="fromVersion">The minimum version to load from. Null means no minimum.</param>
		/// <param name="toVersion">The maximum version to load to. Null means no maximum.</param>
		/// <param name="fromTimestamp">The date/time to load events from.  Null means from the beginning.</param>
		/// <param name="toTimestamp">The date/time to load events to.  Null means to the end.</param>
		/// <returns></returns>
		public IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			if (Guid.Empty == aggregateRootId)
			{
				throw new ArgumentOutOfRangeException("aggregateRootId", aggregateRootId, "AggregateRootId cannot be an empty guid.");
			}

			var version = (fromVersion ?? 1) - 1;
			foreach (var storedEvent in EventStoreProvider.Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp))
			{
				if (version + 1 != storedEvent.Version)
				{
					throw new EventStoreException("Event stream does not contain sequential events.");
				}
				version = storedEvent.Version;

				yield return new StoredEvent() { AggregateRootId = aggregateRootId, AggregateRootType = storedEvent.AggregateRootType, Version = version,	Timestamp = storedEvent.Timestamp, Event = Deserialize(storedEvent.EventType, storedEvent.Data) };
			}
		}

		/// <summary>
		/// Creates a marker that can be used by the provider to load any new events from the last marker.
		/// The marker can be persisted to be used for each subsequent call.
		/// </summary>
		/// <returns>A new marker.</returns>
		public IEventPersisterPosition CreateEventStoreProviderPosition()
		{
			return EventStoreProvider.CreatePosition();
		}

		/// <summary>
		/// Returns any new events since the from position.
		/// </summary>
		/// <param name="batchSize">The maximum number of events that should be returned.</param>
		/// <param name="from">The marker from which events should be returned. If a newly created one is passed in then events are returned from the beginning.</param>
		/// <param name="to">A newly created position marker that is set to the last event that was returned, it should be used for the next call as the <paramref name="from"/> parameter.</param>
		/// <returns>The events loaded.</returns>
		public virtual IEnumerable<StoredEvent> Load(int batchSize, IEventPersisterPosition from, out IEventPersisterPosition to)
		{
			if (1 > batchSize)
			{
				throw new ArgumentOutOfRangeException("batchSize", batchSize, "batchSize cannot be less than 1.");
			}
			if (null == from)
			{
				throw new ArgumentNullException("from");
			}
			to = CreateEventStoreProviderPosition();

			return EventStoreProvider.Load(from, to).Take(batchSize).Select(_eventToStoreFromStoredEvent);
		}

		private StoredEvent EventToStoreFromStoredEvent(EventToStore storedEvent)
		{
			return new StoredEvent() { AggregateRootId = storedEvent.AggregateRootId, AggregateRootType = storedEvent.AggregateRootType, Version = storedEvent.Version, Event = Deserialize(storedEvent.EventType, storedEvent.Data) };
		}

		protected object Deserialize(string eventType, byte[] data)
		{
			if (null == eventType)
			{
				throw new ArgumentNullException("eventType");
			}

			var deserialize = DeserializeMethod.MakeGenericMethod(Type.GetType(eventType));
			object @event = deserialize.Invoke(EventSerializer, new object[] { new MemoryStream(data) });

			EventUpgrader eventUpgrader;
			if (_eventUpgraders.TryGetValue(@event.GetType(), out eventUpgrader))
			{
				@event = eventUpgrader(@event);
			}

			return @event;
		}

		protected byte[] Serialize(object data)
		{
			var stream = new MemoryStream(DefaultSerializationBufferSize);

			var serialize = SerializeMethod.MakeGenericMethod(data.GetType());
			serialize.Invoke(EventSerializer, new object[] { stream, data });

			stream.Flush();
			return stream.ToArray();
		}

		/// <summary>
		/// Registers an event to be upgraded as it loaded from the event store provider.
		/// The event that needs to be upgraded will be passed as the only argument into the constructor of the event it will be upgraded to.
		/// </summary>
		/// <typeparam name="Event">The event type to be upgraded.</typeparam>
		/// <typeparam name="UpgradedEvent">The event type it will be upgraded to. <typeparamref name="UpradedEvent"/> should have a constructor that only takes <typeparamref name="Event"/> as a parameter.</typeparam>
		/// <returns>The event store.</returns>
		public IEventStore Upgrade<Event, UpgradedEvent>()
		{
			var eventType = typeof(Event);
			var upgradedEventType = typeof(UpgradedEvent);

			lock (_eventUpgraders)
			{
				if (_eventUpgraders.ContainsKey(eventType))
				{
					throw new ArgumentException(string.Format("An upgrade has already been registered for {0}.", eventType.Name));
				}

				_eventUpgraders.Add(eventType, ILHelper.CreateEventUpgrader(eventType, upgradedEventType));
			}

			return this;
		}
	}
}
