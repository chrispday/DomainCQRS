using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS.Test
{
	public class MockEventStore : IEventStore
	{
		public IEventStoreProvider EventStoreProvider
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public List<Tuple<Guid, int, string, object>> Saved = new List<Tuple<Guid, int, string, object>>();
		public IEventStore Save(Guid aggregateRootId, int version, Type aggregateRootType, object data)
		{
			Saved.Add(Tuple.Create(aggregateRootId, version, aggregateRootType.AssemblyQualifiedName, data));
			if (null != EventStored)
			{
				EventStored(this, new StoredEvent() { AggregateRootId = aggregateRootId, AggregateRootType = aggregateRootType.AssemblyQualifiedName, Event = data, Timestamp = DateTime.Now, Version = version });
			}
			return this;
		}

		public IEnumerable<StoredEvent> Load(Guid aggregateRootId, int? fromVersion, int? toVersion, DateTime? fromTimestamp, DateTime? toTimestamp)
		{
			return from s in Saved
					 where s.Item1 == aggregateRootId
					 select new StoredEvent() { AggregateRootId = aggregateRootId, AggregateRootType = s.Item3, Version = s.Item2, Event = s.Item4 };
		}

		public ILogger Logger
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public IEventSerializer EventSerializer
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}


		public IEventStoreProviderPosition CreateEventStoreProviderPosition()
		{
			throw new NotImplementedException();
		}

		public IEnumerable<StoredEvent> Load(int batchSize, IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			throw new NotImplementedException();
		}


		public IEventStore Upgrade<Event, UpgradedEvent>()
		{
			throw new NotImplementedException();
		}

		public event EventHandler<StoredEvent> EventStored;
	}

	public static class MockEventStore2Configure
	{
		public static IConfigure MockEventStore2(this IConfigure config)
		{
			config.Registry
				.BuildInstancesOf<IEventStore>()
				.TheDefaultIs(Registry.Instance<IEventStore>()
					.UsingConcreteType<MockEventStore2>()
					.WithProperty("defaultSerializationBufferSize").EqualTo(1024))
				.AsSingletons();
			return config;
		}
	}

	public class MockEventStore2 : EventStore
	{
		public MockEventStore2(ILogger logger, IEventStoreProvider eventStoreProvider, IEventSerializer eventSerializer, int defaultSerializationBufferSize)
			:base(logger, eventStoreProvider, eventSerializer, defaultSerializationBufferSize)
		{}

		public override IEnumerable<StoredEvent> Load(int batchSize, IEventStoreProviderPosition from, IEventStoreProviderPosition to)
		{
			foreach (var storedEvent in EventStoreProvider.Load(from, to))
			{
				var e = new StoredEvent() { AggregateRootId = storedEvent.AggregateRootId, Version = storedEvent.Version, Event = Deserialize(storedEvent.EventType, storedEvent.Data) };
				if (e.Event is MockEvent)
				{
					(e.Event as MockEvent).BatchNo = batchSize;
				}
				yield return e;

				if (0 >= --batchSize)
				{
					break;
				}
			}
		}
	}
}
