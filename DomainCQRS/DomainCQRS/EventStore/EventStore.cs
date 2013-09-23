using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public static class EventStoreConfigure
	{
		public static int DefaultSerializationBufferSize = 1024;
		public static IConfigure EventStore(this IConfigure configure) { return configure.EventStore(DefaultSerializationBufferSize); }
		public static IConfigure EventStore(this IConfigure configure, int defaultSerializationBufferSize)
		{
			var c = configure as Configure;
			c.EventStore = new EventStore(
				c.Logger,
				c.EventStoreProvider,
				c.EventSerializer,
				defaultSerializationBufferSize
				);
			return configure;
		}

		public static IConfigure Upgrade<Event, UpgradedEvent>(this IConfigure configure)
		{
			var c = configure as Configure;
			c.EventStore.Upgrade<Event, UpgradedEvent>();
			return configure;
		}
	}

	public delegate object EventUpgrader(object @event);

	public class EventStore : IEventStore
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly IEventStoreProvider _eventStoreProvider;
		public IEventStoreProvider EventStoreProvider { get { return _eventStoreProvider; } }
		private readonly IEventSerializer _eventSerializer;
		public IEventSerializer EventSerializer { get { return _eventSerializer; } }
		private readonly int _defaultSerializationBufferSize;
		public int DefaultSerializationBufferSize { get { return _defaultSerializationBufferSize; } }

		private MethodInfo _deserialize = typeof(IEventSerializer).GetMethod("Deserialize");
		private MethodInfo _serialize = typeof(IEventSerializer).GetMethod("Serialize");

		public EventStore(ILogger logger, IEventStoreProvider eventStoreProvider, IEventSerializer eventSerializer, int defaultSerializationBufferSize)
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
		}

		public IEventStore Save(Guid aggregateRootId, int version, object data)
		{
			if (Guid.Empty == aggregateRootId)
			{
				throw new ArgumentOutOfRangeException("aggregateRootId", aggregateRootId, "AggregateRootId cannot be an empty guid.");
			}
			if (1 > version)
			{
				throw new ArgumentOutOfRangeException("version", version, "version cannot be less than 1.");
			}
			if (null == data)
			{
				throw new ArgumentNullException("data");
			}

			EventStoreProvider.Save(new EventToStore() { AggregateRootId = aggregateRootId, Version = version, Timestamp = DateTime.Now, EventType = data.GetType().AssemblyQualifiedName, Data = Serialize(data) });
			return this;
		}

		public IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			if (Guid.Empty == aggregateRootId)
			{
				throw new ArgumentOutOfRangeException("aggregateRootId", aggregateRootId, "AggregateRootId cannot be an empty guid.");
			}

			var version = fromVersion ?? 0;
			foreach (var storedEvent in EventStoreProvider.Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp))
			{
				if (version + 1 != storedEvent.Version)
				{
					throw new EventStoreException("Event stream does not contain sequential events.");
				}
				version = storedEvent.Version;

				yield return new StoredEvent() { AggregateRootId = aggregateRootId, Version = version, Event = Deserialize(storedEvent.EventType, storedEvent.Data) };
			}
		}

		public IEventStoreProviderPosition CreateEventStoreProviderPosition()
		{
			return EventStoreProvider.CreatePosition();
		}

		public virtual IEnumerable<StoredEvent> Load(int batchSize, IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			if (1 > batchSize)
			{
				throw new ArgumentOutOfRangeException("batchSize", batchSize, "batchSize cannot be less than 1.");
			}
			if (null == from)
			{
				throw new ArgumentNullException("from");
			}
			if (null == to)
			{
				throw new ArgumentNullException("to");
			}

			foreach (var storedEvent in EventStoreProvider.Load(from, to))
			{
				yield return new StoredEvent() { AggregateRootId = storedEvent.AggregateRootId, Version = storedEvent.Version, Event = Deserialize(storedEvent.EventType, storedEvent.Data) };

				if (0 >= --batchSize)
				{
					break;
				}
			}
		}

		protected object Deserialize(string eventType, byte[] data)
		{
			if (null == eventType)
			{
				throw new ArgumentNullException("eventType");
			}

			var deserialize = _deserialize.MakeGenericMethod(Type.GetType(eventType));
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

			var serialize = _serialize.MakeGenericMethod(data.GetType());
			serialize.Invoke(EventSerializer, new object[] { stream, data });

			stream.Flush();
			return stream.ToArray();
		}

		private Dictionary<Type, EventUpgrader> _eventUpgraders = new Dictionary<Type, EventUpgrader>();
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
