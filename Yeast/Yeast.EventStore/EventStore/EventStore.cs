using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Linq;
using Yeast.EventStore.Common;

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
		private volatile bool _newEvents = true;
		private volatile bool _newEventsSince = false;

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

			_newEvents = true;
			_newEventsSince = true;

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
			return EventStoreProvider.CreateEventStoreProviderPosition();
		}

		public virtual IEnumerable<StoredEvent> Load(int batchSize, IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			if (!_newEvents)
			{
				Logger.Verbose("No new events.");
				//yield break;
			}
			_newEventsSince = false;

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

			_newEvents = _newEventsSince;
		}

		protected object Deserialize(byte[] data)
		{
			return EventSerializer.Deserialize<object>(new MemoryStream(data));
		}

		protected byte[] Serialize<T>(T data)
		{
			var stream = new MemoryStream(DefaultSerializationBufferSize);
			EventSerializer.Serialize(stream, data);
			stream.Flush();
			return stream.ToArray();
		}
	}
}
