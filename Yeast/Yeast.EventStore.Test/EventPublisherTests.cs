using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Yeast.EventStore.Test.Mock;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class EventPublisherTests
	{
		[TestInitialize]
		public void Init()
		{
		}

		[TestMethod]
		public void EventPublisher_Subscribe()
		{
			var config = Configure.With()
				.DebugLogger(true)
				.BinaryFormatterSerializer()
				.MemoryEventStoreProvider()
				.LRUAggregateRootCache(100)
				.EventStore()
				.MessageReceiver()
				.Register<MockCommand, MockAggregateRoot>()
				.MockEventPublisher(100, TimeSpan.FromSeconds(1))
				.Subscribe<MockSubscriber>(Guid.NewGuid());

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
			var config = Configure.With()
				.DebugLogger()
				.BinaryFormatterSerializer()
				.MemoryEventStoreProvider()
				.LRUAggregateRootCache(100)
				.EventStore()
				.MessageReceiver()
				.Register<MockCommand, MockAggregateRoot>()
				.MockEventPublisher(100, TimeSpan.FromSeconds(1))
				.Synchrounous()
				.Subscribe<MockSubscriber>(Guid.NewGuid());

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
			var config = Configure.With();
			config.DebugLogger(true)
			.BinaryFormatterSerializer()
			.MemoryEventStoreProvider()
			.LRUAggregateRootCache(100)
			.EventStore()
			.MessageReceiver()
			.Register<MockCommand, MockAggregateRoot>()
			.MockEventPublisher(2, TimeSpan.FromSeconds(1))
			.Subscribe<MockSubscriber>(Guid.NewGuid());
			(config as Configure).EventStore = new MockEventStore2() { EventSerializer = (config as Configure).EventSerializer, EventStoreProvider = (config as Configure).EventStoreProvider, Logger = (config as Configure).Logger };


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
			var config = Configure.With();
			config.DebugLogger(true)
			.BinaryFormatterSerializer()
			.MemoryEventStoreProvider()
			.LRUAggregateRootCache(100)
			.EventStore()
			.MessageReceiver()
			.Register<MockCommand, MockAggregateRoot>()
			.MockEventPublisher(100, TimeSpan.FromSeconds(1))
			.Subscribe<MockSubscriber>(Guid.NewGuid());

			var publisher = (config as Configure).EventPublisher as MockEventPublisher;
			var logger = (config as Configure).Logger;
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
			var config = Configure.With();
			config.DebugLogger(true)
			.BinaryFormatterSerializer()
			.MemoryEventStoreProvider()
			.LRUAggregateRootCache(100)
			.EventStore()
			.MessageReceiver()
			.Register<MockCommand, MockAggregateRoot>()
			.MockEventPublisher(100, TimeSpan.FromSeconds(1))
			.Synchrounous()
			.Subscribe<MockSubscriber>(Guid.NewGuid())
			.Subscribe<MockSubscriber>(Guid.NewGuid());

			var publisher = (config as Configure).EventPublisher as MockEventPublisher;
			var logger = (config as Configure).Logger;
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
		public void EventPublisher_Subscribe_StopStart()
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			try
			{

				var subId = Guid.NewGuid();
				var config = Configure.With()
					.DebugLogger(true)
					.BinaryFormatterSerializer()
					.FileEventStoreProvider(directory)
					.LRUAggregateRootCache(100)
					.EventStore()
					.MessageReceiver()
					.Register<MockCommand, MockAggregateRoot>()
					.MockEventPublisher(100, TimeSpan.FromSeconds(1))
					.Subscribe<MockSubscriber>(subId);

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

				config.Dispose();

				config = Configure.With()
					.DebugLogger(true)
					.BinaryFormatterSerializer()
					.FileEventStoreProvider(directory)
					.LRUAggregateRootCache(100)
					.EventStore()
					.MessageReceiver()
					.Register<MockCommand, MockAggregateRoot>()
					.MockEventPublisher(100, TimeSpan.FromSeconds(1))
					.Subscribe<MockSubscriber>(subId);

				publisher = (config as Configure).EventPublisher as MockEventPublisher;
				Assert.AreEqual(1, publisher.Subscribers.Count);
				subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 4 });
				subscriber.ReceivedEvent.WaitOne(TimeSpan.FromSeconds(10));

				Assert.AreEqual(1, subscriber.Received.Count);
				Assert.IsInstanceOfType(subscriber.Received[0], typeof(MockEvent));
				@event = subscriber.Received[0] as MockEvent;
				Assert.AreEqual(id, @event.AggregateRootId);
				Assert.AreEqual(4, @event.Increment);

				config.Subscribe<MockSubscriber>(Guid.NewGuid());
				Assert.AreEqual(2, publisher.Subscribers.Count);
				var subscriber2 = publisher.Subscribers.Skip(1).First().Value.Item1 as MockSubscriber;
				var c = 100;
				while (subscriber2.Received.Count < 2 && --c > 0)
				{
					System.Threading.Thread.Sleep(100);
				}
				Assert.AreEqual(2, subscriber2.Received.Count);

				Assert.IsInstanceOfType(subscriber2.Received[0], typeof(MockEvent));
				@event = subscriber2.Received[0] as MockEvent;
				Assert.AreEqual(id, @event.AggregateRootId);
				Assert.AreEqual(5, @event.Increment);

				Assert.IsInstanceOfType(subscriber2.Received[1], typeof(MockEvent));
				@event = subscriber2.Received[1] as MockEvent;
				Assert.AreEqual(id, @event.AggregateRootId);
				Assert.AreEqual(4, @event.Increment);

				config.Dispose();
			}
			finally
			{
				try
				{
					Directory.Delete(directory);
				}
				catch { }
			}
		}
	}
}
