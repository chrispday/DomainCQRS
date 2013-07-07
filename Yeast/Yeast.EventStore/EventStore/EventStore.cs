using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Linq;

namespace Yeast.EventStore
{
	public class EventStore : IEventStore
	{
		public IEventSerializer Serializer { get; set; }
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

		public IEventStore Save<T>(Guid aggregateId, int version, T data)
		{
			EventStoreProvider.Save(new EventToStore() { AggregateId = aggregateId, Version = version, Timestamp = DateTime.Now, Data = Serialize<T>(data) });
			return this;
		}

		public IEnumerable<StoredEvent> Load(Guid aggregateId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			return
				from storedEvent in EventStoreProvider.Load(aggregateId, fromVersion, toVersion, fromTimestamp, toTimestamp)
				orderby storedEvent.Version
				select new StoredEvent() { AggregateId = aggregateId, Version = storedEvent.Version, Event = Deserialize(storedEvent.Data) };
		}

		private object Deserialize(byte[] data)
		{
			return Serializer.Deserialize<object>(new MemoryStream(data));
		}

		private byte[] Serialize<T>(T data)
		{
			var stream = new MemoryStream(SerializationDefaultBufferSize);
			Serializer.Serialize(stream, data);
			stream.Flush();
			return stream.ToArray();
		}

	}
}
