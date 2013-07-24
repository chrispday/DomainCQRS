using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Linq;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public class EventStore : IEventStore
	{
		public ILogger Logger { get; set; }
		public IEventSerializer EventSerializer { get; set; }
		public int SerializationDefaultBufferSize { get; set; }
		public IEventStoreProvider _eventStoreProvider;
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
			SerializationDefaultBufferSize = 1024;
		}

		public static IEventStore Current { get; set; }

		public IEventStore Save<T>(Guid aggregateRootId, int version, T data)
		{
			EventStoreProvider.Save(new EventToStore() { AggregateRootId = aggregateRootId, Version = version, Timestamp = DateTime.Now, Data = Serialize<T>(data) });
			return this;
		}

		public IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			var version = -1;
			foreach (var storedEvent in EventStoreProvider.Load(aggregateRootId, fromVersion, toVersion, fromTimestamp, toTimestamp))
			{
				if (version != -1
					&& version + 1 != storedEvent.Version)
				{
					throw new EventStoreException("Event stream does not contain concurrent events.");
				}
				version = storedEvent.Version;

				yield return new StoredEvent() { AggregateRootId = aggregateRootId, Version = version, Event = Deserialize(storedEvent.Data) };
			}
		}

		private object Deserialize(byte[] data)
		{
			return EventSerializer.Deserialize<object>(new MemoryStream(data));
		}

		private byte[] Serialize<T>(T data)
		{
			var stream = new MemoryStream(SerializationDefaultBufferSize);
			EventSerializer.Serialize(stream, data);
			stream.Flush();
			return stream.ToArray();
		}

	}
}
