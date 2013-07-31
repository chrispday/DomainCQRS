using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yeast.EventStore.Test.Mock;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class EventPublisherTests
	{
		[TestMethod]
		public void EventPublisher_Subscribe()
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			var config = Configure.With()
				.DebugLogger()
				.BinaryFormatterSerializer()
				.FileEventStoreProvider(directory)
				.LRUAggregateRootCache(100)
				.MessageReceiver()
				.Register<MockCommand, MockAggregateRoot>()
				.MockEventPublisher()
				.Subscribe<MockSubscriber>(Guid.NewGuid());

			try
			{
				var publisher = (config as Configure).EventPublisher as MockEventPublisher;
				Assert.AreEqual(1, publisher.Subscribers.Count);
				var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

				var id = Guid.NewGuid();
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
				subscriber.ReceivedEvent.WaitOne(-1);

				Assert.AreEqual(1, subscriber.Received.Count);
				Assert.IsInstanceOfType(subscriber.Received[0], typeof(StoredEvent));
				var storedEvent = subscriber.Received[0] as StoredEvent;
				Assert.IsInstanceOfType(storedEvent.Event, typeof(MockEvent));
				var @event = storedEvent.Event as MockEvent;
				Assert.AreEqual(id, @event.AggregateRootId);
				Assert.AreEqual(5, @event.Increment);

			}
			finally
			{
				try
				{
					config.Dispose();
				}
				catch { }
				try
				{
					Directory.Delete(directory, true);
				}
				catch { }
			}
		}

		[TestMethod]
		public void EventPublisher_Subscribe_MultipleBatches()
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			MockSubscriber.SignalOnCount = 5;

			var config = Configure.With();
			(config as Configure).EventStore = new MockEventStore2();
			config.DebugLogger()
			.BinaryFormatterSerializer()
			.FileEventStoreProvider(directory)
			.LRUAggregateRootCache(100)
			.MessageReceiver()
			.Register<MockCommand, MockAggregateRoot>()
			.MockEventPublisher(2)
			.Subscribe<MockSubscriber>(Guid.NewGuid());


			try
			{
				var publisher = (config as Configure).EventPublisher as MockEventPublisher;
				Assert.AreEqual(1, publisher.Subscribers.Count);
				var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

				var id = Guid.NewGuid();
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 4 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 3 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 2 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 });
				subscriber.ReceivedEvent.WaitOne(TimeSpan.FromMinutes(1));

				Assert.AreEqual(5, subscriber.Received.Count);
				Assert.AreEqual(5, ((subscriber.Received[0] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(4, ((subscriber.Received[1] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(3, ((subscriber.Received[2] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(2, ((subscriber.Received[3] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(1, ((subscriber.Received[4] as StoredEvent).Event as MockEvent).Increment);

				Assert.AreEqual(2, ((subscriber.Received[0] as StoredEvent).Event as MockEvent).BatchNo);
				Assert.AreEqual(1, ((subscriber.Received[1] as StoredEvent).Event as MockEvent).BatchNo);
				Assert.AreEqual(2, ((subscriber.Received[2] as StoredEvent).Event as MockEvent).BatchNo);
				Assert.AreEqual(1, ((subscriber.Received[3] as StoredEvent).Event as MockEvent).BatchNo);
				Assert.AreEqual(2, ((subscriber.Received[4] as StoredEvent).Event as MockEvent).BatchNo);
			}
			finally
			{
				try
				{
					config.Dispose();
				}
				catch { }
				try
				{
					Directory.Delete(directory, true);
				}
				catch { }
			}
		}

		[TestMethod]
		public void EventPublisher_Subscribe_MultipleSubscribers()
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

			MockSubscriber.SignalOnCount = 5;

			var config = Configure.With();
			(config as Configure).EventStore = new MockEventStore2();
				config.DebugLogger()
				.BinaryFormatterSerializer()
				.FileEventStoreProvider(directory)
				.LRUAggregateRootCache(100)
				.MessageReceiver()
				.Register<MockCommand, MockAggregateRoot>()
				.MockEventPublisher()
				.Subscribe<MockSubscriber>(Guid.NewGuid());


			try
			{
				var publisher = (config as Configure).EventPublisher as MockEventPublisher;
				Assert.AreEqual(1, publisher.Subscribers.Count);
				var subscriber = publisher.Subscribers.First().Value.Item1 as MockSubscriber;

				var id = Guid.NewGuid();
				var messageReceiver = (config as Configure).MessageReceiver;
				messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
				messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 4 });
				messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 3 });
				messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 2 });
				messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 });
				subscriber.ReceivedEvent.WaitOne(TimeSpan.FromMinutes(1));

				Assert.AreEqual(5, subscriber.Received.Count);
				Assert.AreEqual(5, ((subscriber.Received[0] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(4, ((subscriber.Received[1] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(3, ((subscriber.Received[2] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(2, ((subscriber.Received[3] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(1, ((subscriber.Received[4] as StoredEvent).Event as MockEvent).Increment);

				config.Subscribe<MockSubscriber>(Guid.NewGuid());
				Assert.AreEqual(2, publisher.Subscribers.Count);
				var subscriber2 = publisher.Subscribers.Skip(1).First().Value.Item1 as MockSubscriber;
				subscriber2.ReceivedEvent.WaitOne(TimeSpan.FromMinutes(1));

				Assert.AreEqual(5, subscriber2.Received.Count);
				Assert.AreEqual(5, ((subscriber2.Received[0] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(4, ((subscriber2.Received[1] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(3, ((subscriber2.Received[2] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(2, ((subscriber2.Received[3] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(1, ((subscriber2.Received[4] as StoredEvent).Event as MockEvent).Increment);

				MockSubscriber.SignalOnCount = 6;
				subscriber.ReceivedEvent.Reset();
				subscriber2.ReceivedEvent.Reset();
				messageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 10 });
				subscriber.ReceivedEvent.WaitOne(TimeSpan.FromMinutes(1));
				subscriber2.ReceivedEvent.WaitOne(TimeSpan.FromMinutes(1));

				Assert.AreEqual(6, subscriber.Received.Count);
				Assert.AreEqual(6, subscriber2.Received.Count);
				Assert.AreEqual(10, ((subscriber.Received[5] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(10, ((subscriber2.Received[5] as StoredEvent).Event as MockEvent).Increment);
			}
			finally
			{
				try
				{
					config.Dispose();
				}
				catch { }
				try
				{
					Directory.Delete(directory, true);
				}
				catch { }
			}
		}
	}
}
