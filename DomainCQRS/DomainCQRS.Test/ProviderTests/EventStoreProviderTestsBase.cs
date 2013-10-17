using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DomainCQRS.Test.Mock;
using System.Threading;

namespace DomainCQRS.Test
{
	public abstract class EventStoreProviderTestsBase
	{
		protected abstract IConfigure RegisterProvider(IConfigure configure);
		protected abstract IEventPersister CreateProvider();
		protected abstract bool ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder { get; }

		[TestMethod]
		public void EventStoreProvider_Save()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4, 5, 6, 7 } };
			provider.Save(EventToStore2);

			var results = provider.Load(EventToStore.AggregateRootId, null, null, null, null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(EventToStore.AggregateRootId, results[0].AggregateRootId);
			Assert.AreEqual(typeof(MockAggregateRoot).AssemblyQualifiedName, results[0].AggregateRootType);
			Assert.AreEqual(EventToStore.Version, results[0].Version);
			Assert.AreEqual(EventToStore.EventType, results[0].EventType);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(results[0].Data));
			Assert.AreEqual(EventToStore2.AggregateRootId, results[1].AggregateRootId);
			Assert.AreEqual(EventToStore2.Version, results[1].Version);
			Assert.AreEqual(typeof(MockAggregateRoot).AssemblyQualifiedName, results[1].AggregateRootType);
			Assert.AreEqual(EventToStore2.EventType, results[0].EventType);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(results[1].Data));

			provider.Dispose();
		}

		[TestMethod, ExpectedException(typeof(ConcurrencyException))]
		public void EventStoreProvider_Save_VersionExists()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			provider.Save(EventToStore);
		}

		[TestMethod]
		public void EventStoreProvider_Load()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = EventToStore.AggregateRootType, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4, 5, 6, 7 } };
			provider.Save(EventToStore2);
			var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = EventToStore.AggregateRootType, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 3, 5 } };
			provider.Save(EventToStore3);

			var events = provider.Load(EventToStore.AggregateRootId, null, null, null, null);
			Assert.AreEqual(3, events.Count());
			var se1 = events.First(se => 1 == se.Version);
			Assert.IsNotNull(se1);
			Assert.AreEqual(EventToStore.AggregateRootId, se1.AggregateRootId);
			Assert.AreEqual(EventToStore.AggregateRootType, se1.AggregateRootType);
			Assert.AreEqual(EventToStore.Version, se1.Version);
			Assert.AreEqual(EventToStore.EventType, se1.EventType);
			Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

			var se2 = events.First(se => 2 == se.Version);
			Assert.IsNotNull(se2);
			Assert.AreEqual(EventToStore2.AggregateRootId, se2.AggregateRootId);
			Assert.AreEqual(EventToStore2.AggregateRootType, se2.AggregateRootType);
			Assert.AreEqual(EventToStore2.Version, se2.Version);
			Assert.AreEqual(EventToStore2.EventType, se2.EventType);
			Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

			var se3 = events.First(se => 3 == se.Version);
			Assert.IsNotNull(se3);
			Assert.AreEqual(EventToStore3.AggregateRootId, se3.AggregateRootId);
			Assert.AreEqual(EventToStore3.AggregateRootType, se3.AggregateRootType);
			Assert.AreEqual(EventToStore3.Version, se3.Version);
			Assert.AreEqual(EventToStore3.EventType, se3.EventType);
			Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
		}

		[TestMethod]
		public void EventStoreProvider_LoadUnknownId()
		{
			var provider = CreateProvider().EnsureExists();
			var results = provider.Load(Guid.NewGuid(), null, null, null, null).ToList();
			Assert.AreEqual(0, results.Count);
		}

		[TestMethod]
		public void EventStoreProvider_Load_FromVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
			provider.Save(EventToStore);
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = EventToStore.AggregateRootType, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2 } });
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = EventToStore.AggregateRootType, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3 } });
			provider.Save(new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = EventToStore.AggregateRootType, Version = 4, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4 } });

			var events = provider.Load(EventToStore.AggregateRootId, 3, null, null, null);
			Assert.AreEqual(2, events.Count());
			Assert.IsTrue(events.All(se => 3 <= se.Version));
		}

		[TestMethod]
		public void EventStoreProvider_LoadAfterSaveOutOfOrder()
		{
			try
			{
				var provider = CreateProvider().EnsureExists();

				var EventToStore = new EventToStore() { AggregateRootId = Guid.NewGuid(), AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } };
				var EventToStore3 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = EventToStore.AggregateRootType, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 3, 5 } };
				provider.Save(EventToStore3);
				var EventToStore2 = new EventToStore() { AggregateRootId = EventToStore.AggregateRootId, AggregateRootType = EventToStore.AggregateRootType, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 4, 5, 6, 7 } };
				provider.Save(EventToStore2);
				provider.Save(EventToStore);

				var events = provider.Load(EventToStore.AggregateRootId, null, null, null, null);
				Assert.AreEqual(3, events.Count());
				var se1 = events.First(se => 1 == se.Version);
				Assert.IsNotNull(se1);
				Assert.AreEqual(EventToStore.AggregateRootId, se1.AggregateRootId);
				Assert.AreEqual(EventToStore.AggregateRootType, se1.AggregateRootType);
				Assert.AreEqual(EventToStore.Version, se1.Version);
				Assert.AreEqual(EventToStore.EventType, se1.EventType);
				Assert.IsTrue(EventToStore.Data.SequenceEqual(se1.Data));

				var se2 = events.First(se => 2 == se.Version);
				Assert.IsNotNull(se2);
				Assert.AreEqual(EventToStore2.AggregateRootId, se2.AggregateRootId);
				Assert.AreEqual(EventToStore2.AggregateRootType, se2.AggregateRootType);
				Assert.AreEqual(EventToStore2.Version, se2.Version);
				Assert.AreEqual(EventToStore2.EventType, se2.EventType);
				Assert.IsTrue(EventToStore2.Data.SequenceEqual(se2.Data));

				var se3 = events.First(se => 3 == se.Version);
				Assert.IsNotNull(se3);
				Assert.AreEqual(EventToStore3.AggregateRootId, se3.AggregateRootId);
				Assert.AreEqual(EventToStore3.AggregateRootType, se3.AggregateRootType);
				Assert.AreEqual(EventToStore3.Version, se3.Version);
				Assert.AreEqual(EventToStore3.EventType, se3.EventType);
				Assert.IsTrue(EventToStore3.Data.SequenceEqual(se3.Data));
			}
			catch (ConcurrencyException)
			{
				if (!ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder)
				{
					throw;
				}
				return;
			}
			if (ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder)
			{
				Assert.Fail("Expected ConcurrencyException.");
			}
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, 2, null, null, null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(2, results[0].Version);
			Assert.AreEqual(3, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MaxVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, 2, null, null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(2, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinMaxVersion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, 2, 2, null, null).ToList();
			Assert.AreEqual(1, results.Count);
			Assert.AreEqual(2, results[0].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinTimestamp()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = new DateTime(2000, 1, 1), EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = new DateTime(2000, 1, 2), EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = new DateTime(2000, 1, 3), EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, null, new DateTime(2000, 1, 2).AddSeconds(-1), null).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(2, results[0].Version);
			Assert.AreEqual(3, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MaxTimestamp()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = new DateTime(2000, 1, 1), EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = new DateTime(2000, 1, 2), EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = new DateTime(2000, 1, 3), EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, null, null, new DateTime(2000, 1, 2).AddSeconds(1)).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(2, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_Load_MinMaxTimestamp()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = new DateTime(2000, 1, 1), EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = new DateTime(2000, 1, 2), EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = new DateTime(2000, 1, 3), EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var results = provider.Load(id, null, null, new DateTime(2000, 1, 2).AddSeconds(-1), new DateTime(2000, 1, 2).AddSeconds(1)).ToList();
			Assert.AreEqual(1, results.Count);
			Assert.AreEqual(2, results[0].Version);
		}

		[TestMethod]
		public void EventStoreProvider_LoadFromPostion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var from = provider.CreatePosition();
			var to = provider.CreatePosition();

			var results = provider.Load(from, to).ToList();
			Assert.AreEqual(3, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(id, results[0].AggregateRootId);
			Assert.AreEqual(typeof(MockAggregateRoot).AssemblyQualifiedName, results[0].AggregateRootType);
			Assert.IsTrue(Enumerable.SequenceEqual(new byte[] { 1, 2, 3 }, results[0].Data));
			Assert.AreEqual(typeof(byte[]).FullName, results[0].EventType);

			Assert.AreEqual(2, results[1].Version);
			Assert.AreEqual(id, results[1].AggregateRootId);
			Assert.AreEqual(typeof(MockAggregateRoot).AssemblyQualifiedName, results[1].AggregateRootType);
			Assert.IsTrue(Enumerable.SequenceEqual(new byte[] { 2, 3, 4 }, results[1].Data));
			Assert.AreEqual(typeof(byte[]).FullName, results[1].EventType);

			Assert.AreEqual(3, results[2].Version);
			Assert.AreEqual(id, results[2].AggregateRootId);
			Assert.AreEqual(typeof(MockAggregateRoot).AssemblyQualifiedName, results[2].AggregateRootType);
			Assert.IsTrue(Enumerable.SequenceEqual(new byte[] { 3, 4, 5 }, results[2].Data));
			Assert.AreEqual(typeof(byte[]).FullName, results[2].EventType);

			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 4, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 5, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			from = to;
			to = provider.CreatePosition();

			results = provider.Load(from, to).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(4, results[0].Version);
			Assert.AreEqual(5, results[1].Version);
		}

		[TestMethod]
		public void EventStoreProvider_SaveAndLoadPostion()
		{
			var provider = CreateProvider().EnsureExists();
			var id = Guid.NewGuid();
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 1, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 1, 2, 3 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 2, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 3, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			var from = provider.CreatePosition();
			var to = provider.CreatePosition();

			var results = provider.Load(from, to).ToList();
			Assert.AreEqual(3, results.Count);
			Assert.AreEqual(1, results[0].Version);
			Assert.AreEqual(2, results[1].Version);
			Assert.AreEqual(3, results[2].Version);

			var subId = Guid.NewGuid();
			provider.SavePosition(subId, to);

			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 4, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 2, 3, 4 } });
			provider.Save(new EventToStore() { AggregateRootId = id, AggregateRootType = typeof(MockAggregateRoot).AssemblyQualifiedName, Version = 5, Timestamp = DateTime.Now, EventType = typeof(byte[]).FullName, Data = new byte[] { 3, 4, 5 } });

			from = provider.LoadPosition(subId);
			to = provider.CreatePosition();

			results = provider.Load(from, to).ToList();
			Assert.AreEqual(2, results.Count);
			Assert.AreEqual(4, results[0].Version);
			Assert.AreEqual(5, results[1].Version);
		}

		[TestMethod]
		public void EventPublisher_Subscribe()
		{
			var config = RegisterProvider(Configure.With())
				.DebugLogger(true)
				.BinaryFormatterSerializer()
				.LRUAggregateRootCache(100)
				.EventStore()
				.MockEventPublisher(100, TimeSpan.FromSeconds(1))
				.MessageReceiver()
				.Build()
					.Subscribe<MockSubscriber>(Guid.NewGuid())
					.Register<MockCommand, MockAggregateRoot>();

			var publisher = (config as Configure).EventPublisher as MockEventPublisher;
			Assert.AreEqual(1, publisher.Subscribers.Count);
			var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

			var id = Guid.NewGuid();
			(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
			subscriber.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));

			Assert.AreEqual(1, subscriber.Received.Count);
			Assert.IsInstanceOfType(subscriber.Received[0], typeof(MockEvent));
			var @event = subscriber.Received[0] as MockEvent;
			Assert.AreEqual(id, @event.AggregateRootId);
			Assert.AreEqual(5, @event.Increment);
		}

		[TestMethod]
		public void EventPublisher_Subscribe_Synchro()
		{
			var config = RegisterProvider(Configure.With())
				.DebugLogger(true)
				.BinaryFormatterSerializer()
				.LRUAggregateRootCache(100)
				.EventStore()
				.MockEventPublisher(100, TimeSpan.FromSeconds(1))
				.MessageReceiver()
				.Synchrounous()
				.Build()
					.Subscribe<MockSubscriber>(Guid.NewGuid())
					.Register<MockCommand, MockAggregateRoot>();

			var publisher = (config as Configure).EventPublisher as MockEventPublisher;
			Assert.AreEqual(1, publisher.Subscribers.Count);
			var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

			var id = Guid.NewGuid();
			(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });

			Assert.AreEqual(1, subscriber.Received.Count);
			Assert.IsInstanceOfType(subscriber.Received[0], typeof(MockEvent));
			var @event = subscriber.Received[0] as MockEvent;
			Assert.AreEqual(id, @event.AggregateRootId);
			Assert.AreEqual(5, @event.Increment);
		}

		[TestMethod]
		public void EventPublisher_Subscribe_MultipleBatches()
		{
			var config = RegisterProvider(Configure.With())
				.DebugLogger(true)
				.BinaryFormatterSerializer()
				.LRUAggregateRootCache(100)
				.MockEventStore2()
				.MockEventPublisher(2, TimeSpan.FromSeconds(1))
				.MessageReceiver()
				.Build()
					.Subscribe<MockSubscriber>(Guid.NewGuid())
					.Register<MockCommand, MockAggregateRoot>();


			var publisher = (config as Configure).EventPublisher as MockEventPublisher;
			Assert.AreEqual(1, publisher.Subscribers.Count);
			var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;
			subscriber.SignalOnCount = 5;

			var id = Guid.NewGuid();
			(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
			(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 4 });
			(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 3 });
			(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 2 });
			(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 });
			subscriber.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));

			System.Threading.Thread.Sleep(1000);

			Assert.AreEqual(5, subscriber.Received.Count);
			Assert.AreEqual(5, (subscriber.Received[0] as MockEvent).Increment);
			Assert.AreEqual(4, (subscriber.Received[1] as MockEvent).Increment);
			Assert.AreEqual(3, (subscriber.Received[2] as MockEvent).Increment);
			Assert.AreEqual(2, (subscriber.Received[3] as MockEvent).Increment);
			Assert.AreEqual(1, (subscriber.Received[4] as MockEvent).Increment);

			Assert.AreEqual(2, (subscriber.Received[0] as MockEvent).BatchNo);
			Assert.AreEqual(1, (subscriber.Received[1] as MockEvent).BatchNo);
			Assert.AreEqual(2, (subscriber.Received[2] as MockEvent).BatchNo);
			Assert.AreEqual(1, (subscriber.Received[3] as MockEvent).BatchNo);
			Assert.AreEqual(2, (subscriber.Received[4] as MockEvent).BatchNo);
		}

		[TestMethod]
		public void EventPublisher_Subscribe_MultipleSubscribers()
		{
			var config = RegisterProvider(Configure.With())
				.DebugLogger(true)
				.BinaryFormatterSerializer()
				.LRUAggregateRootCache(100)
				.EventStore()
				.MessageReceiver()
				.MockEventPublisher(100, TimeSpan.FromSeconds(1))
				.Build()
					.Register<MockCommand, MockAggregateRoot>()
					.Subscribe<MockSubscriber>(Guid.NewGuid());

			var publisher = (config as Configure).EventPublisher as MockEventPublisher;
			var logger = publisher.Logger;
			Assert.AreEqual(1, publisher.Subscribers.Count);
			var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;
			subscriber.SignalOnCount = 5;

			var id = Guid.NewGuid();
			var messageReceiver = (config as Configure).MessageReceiver;
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 4 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 3 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 2 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 });
			subscriber.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));

			Assert.AreEqual(5, subscriber.Received.Count);
			Assert.AreEqual(5, (subscriber.Received[0] as MockEvent).Increment);
			Assert.AreEqual(4, (subscriber.Received[1] as MockEvent).Increment);
			Assert.AreEqual(3, (subscriber.Received[2] as MockEvent).Increment);
			Assert.AreEqual(2, (subscriber.Received[3] as MockEvent).Increment);
			Assert.AreEqual(1, (subscriber.Received[4] as MockEvent).Increment);

			config.Subscribe<MockSubscriber>(Guid.NewGuid());
			Assert.AreEqual(2, publisher.Subscribers.Count);
			var subscriber2 = publisher.Subscribers.Skip(1).First().Value.Item1 as MockSubscriber;
			subscriber2.SignalOnCount = 5;
			subscriber2.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));

			Assert.AreEqual(5, subscriber2.Received.Count);
			Assert.AreEqual(5, (subscriber2.Received[0] as MockEvent).Increment);
			Assert.AreEqual(4, (subscriber2.Received[1] as MockEvent).Increment);
			Assert.AreEqual(3, (subscriber2.Received[2] as MockEvent).Increment);
			Assert.AreEqual(2, (subscriber2.Received[3] as MockEvent).Increment);
			Assert.AreEqual(1, (subscriber2.Received[4] as MockEvent).Increment);

			//System.Threading.Thread.Sleep(5000);

			subscriber.SignalOnCount = 6;
			subscriber2.SignalOnCount = 6;

			subscriber.ReceivedEvent.Reset();
			subscriber2.ReceivedEvent.Reset();
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 10 });
			logger.Information("*****Sent Command");
			subscriber.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));
			subscriber2.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));

			Assert.AreEqual(6, subscriber.Received.Count);
			Assert.AreEqual(6, subscriber2.Received.Count);
			Assert.AreEqual(10, (subscriber.Received[5] as MockEvent).Increment);
			Assert.AreEqual(10, (subscriber2.Received[5] as MockEvent).Increment);
		}

		[TestMethod]
		public void EventPublisher_Subscribe_MultipleSubscribers_Synchro()
		{
			var config = RegisterProvider(Configure.With())
				.DebugLogger(true)
				.BinaryFormatterSerializer()
				.LRUAggregateRootCache(100)
				.EventStore()
				.MockEventPublisher(100, TimeSpan.FromSeconds(1))
				.MessageReceiver()
				.Synchrounous()
				.Build()
					.Subscribe<MockSubscriber>(Guid.NewGuid())
					.Subscribe<MockSubscriber>(Guid.NewGuid())
					.Register<MockCommand, MockAggregateRoot>();

			var publisher = config.EventPublisher as MockEventPublisher;
			var logger = publisher.Logger;
			Assert.AreEqual(2, publisher.Subscribers.Count);
			var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

			var id = Guid.NewGuid();
			var messageReceiver = (config as Configure).MessageReceiver;
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 4 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 3 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 2 });
			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 });

			Assert.AreEqual(5, subscriber.Received.Count);
			Assert.AreEqual(5, (subscriber.Received[0] as MockEvent).Increment);
			Assert.AreEqual(4, (subscriber.Received[1] as MockEvent).Increment);
			Assert.AreEqual(3, (subscriber.Received[2] as MockEvent).Increment);
			Assert.AreEqual(2, (subscriber.Received[3] as MockEvent).Increment);
			Assert.AreEqual(1, (subscriber.Received[4] as MockEvent).Increment);

			Assert.AreEqual(2, publisher.Subscribers.Count);
			var subscriber2 = publisher.Subscribers.Skip(1).First().Value.Item1 as MockSubscriber;

			Assert.AreEqual(5, subscriber2.Received.Count);
			Assert.AreEqual(5, (subscriber2.Received[0] as MockEvent).Increment);
			Assert.AreEqual(4, (subscriber2.Received[1] as MockEvent).Increment);
			Assert.AreEqual(3, (subscriber2.Received[2] as MockEvent).Increment);
			Assert.AreEqual(2, (subscriber2.Received[3] as MockEvent).Increment);
			Assert.AreEqual(1, (subscriber2.Received[4] as MockEvent).Increment);

			messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 10 });
			logger.Information("*****Sent Command");
			subscriber.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));
			subscriber2.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));

			Assert.AreEqual(6, subscriber.Received.Count);
			Assert.AreEqual(6, subscriber2.Received.Count);
			Assert.AreEqual(10, (subscriber.Received[5] as MockEvent).Increment);
			Assert.AreEqual(10, (subscriber2.Received[5] as MockEvent).Increment);
		}

		[TestMethod]
		public void SagaTest_Saga()
		{
			var config = RegisterProvider(Configure.With())
				.DebugLogger(true)
				.BinaryFormatterSerializer()
				.EventStore()
				.NoAggregateRootCache()
				.MessageReceiver()
				.EventPublisher()
				.SagaPublisher()
				.Build()
					.Register<MockSagaCommand, MockSagaAggregateRoot>()
					.Register<MockSagaEvent, MockSaga>()
					.Saga<MockSagaEvent>();
			try
			{

				MockSaga.EventsHandled = 0;
				MockSaga.Signal.Reset();
				MockSaga.SignalOnEventsHandled = 2;

				Guid aggregateRootId = Guid.NewGuid();
				config.MessageReceiver.Receive(new MockSagaCommand() { AggregateRootId = aggregateRootId, Message = "1" });
				config.MessageReceiver.Receive(new MockSagaCommand() { AggregateRootId = aggregateRootId, Message = "2" });

				MockSaga.Signal.WaitOne(60000);
				Thread.Sleep(1000);

				var events = config.MessageReceiver.EventStore.Load(aggregateRootId, null, null, null, null).ToList();
				Assert.AreEqual(2, events.Count);
				Assert.AreEqual("1", (events[0].Event as MockSagaEvent).Message);
				var sagaId = (events[0].Event as MockSagaEvent).AggregateRootId;
				Assert.AreEqual("2", (events[1].Event as MockSagaEvent).Message);
				Assert.AreEqual(sagaId, (events[1].Event as MockSagaEvent).AggregateRootId);

				events = config.MessageReceiver.EventStore.Load(sagaId, null, null, null, null).ToList();
				Assert.AreEqual(2, events.Count);
				Assert.AreEqual("Saga 1", (events[0].Event as MockSagaEvent2).Message);
				Assert.AreEqual("Saga 2", (events[1].Event as MockSagaEvent2).Message);
			}
			finally
			{
				config.Dispose();
			}
		}
	}
}
