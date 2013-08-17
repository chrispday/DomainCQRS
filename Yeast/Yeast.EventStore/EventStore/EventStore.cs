using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

using Yeast.EventStore.Common;
using System.Reflection.Emit;

namespace Yeast.EventStore
{
	public static class EventStoreConfigure
	{
		public static int DefaultSerializationBufferSize = 1024;
		public static IConfigure EventStore(this IConfigure configure) { return configure.EventStore(DefaultSerializationBufferSize); }
		public static IConfigure EventStore(this IConfigure configure, int defaultSerializationBufferSize)
		{
			if (1 > defaultSerializationBufferSize)
			{
				throw new ArgumentOutOfRangeException("defaultSerializationBufferSize", defaultSerializationBufferSize, "defaultSerializationBufferSize cannot be less than 1.");
			}

			var c = configure as Configure;
			c.EventStore = new EventStore() { EventSerializer = c.EventSerializer, EventStoreProvider = c.EventStoreProvider, Logger = c.Logger, DefaultSerializationBufferSize = defaultSerializationBufferSize };
			return configure;
		}

		public static IConfigure Upgrade<Event, UpgradedEvent>(this IConfigure configure)
		{
			var c = configure as Configure;
			c.EventStore.Upgrade<Event, UpgradedEvent>();
			return configure;
		}
	}

	public class EventStore : IEventStore
	{
		public ILogger Logger { get; set; }
		public IEventSerializer EventSerializer { get; set; }
		public int DefaultSerializationBufferSize { get; set; }
		private IEventStoreProvider _eventStoreProvider;
		public IEventStoreProvider EventStoreProvider
		{
			get { return _eventStoreProvider; }
			set
			{
				if (_eventStoreProvider != value)
				{
					_eventStoreProvider = value;
					_eventStoreProvider.EnsureExists();
				}
			}
		}

		public EventStore()
		{
			DefaultSerializationBufferSize = EventStoreConfigure.DefaultSerializationBufferSize;
		}

		public IEventStore Save<T>(Guid aggregateRootId, int version, T data)
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

			EventStoreProvider.Save(new EventToStore() { AggregateRootId = aggregateRootId, Version = version, Timestamp = DateTime.Now, Data = Serialize<T>(data) });
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

				yield return new StoredEvent() { AggregateRootId = aggregateRootId, Version = version, Event = Deserialize(storedEvent.Data) };
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
				yield return new StoredEvent() { AggregateRootId = storedEvent.AggregateRootId, Version = storedEvent.Version, Event = Deserialize(storedEvent.Data) };

				if (0 >= --batchSize)
				{
					break;
				}
			}
		}

		protected object Deserialize(byte[] data)
		{
			var @event = EventSerializer.Deserialize<object>(new MemoryStream(data));
			
			EventUpgrader eventUpgrader;
			if (_eventUpgraders.TryGetValue(@event.GetType(), out eventUpgrader))
			{
				@event = eventUpgrader(@event);
			}

			return @event;
		}

		protected byte[] Serialize<T>(T data)
		{
			var stream = new MemoryStream(DefaultSerializationBufferSize);
			EventSerializer.Serialize(stream, data);
			stream.Flush();
			return stream.ToArray();
		}

		private delegate object EventUpgrader(object @event);
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

				var upgradedEventConstructor = upgradedEventType.GetConstructor(new Type[] { eventType });
				if (null == upgradedEventConstructor)
				{
					throw new ArgumentOutOfRangeException(string.Format("{0} does not have a constructor \"public {0}({1})\".", upgradedEventType.Name, eventType.Name));
				}

				var dynamicMethod = new DynamicMethod(upgradedEventType.Name + "_Upgrade", typeof(object), new Type[] { typeof(object) });
				var ilGenerator = dynamicMethod.GetILGenerator();
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Castclass, eventType);
				ilGenerator.Emit(OpCodes.Newobj, upgradedEventConstructor);
				ilGenerator.Emit(OpCodes.Ret);
				_eventUpgraders.Add(eventType, (EventUpgrader)dynamicMethod.CreateDelegate(typeof(EventUpgrader)));
			}

			return this;
		}
	}
}
