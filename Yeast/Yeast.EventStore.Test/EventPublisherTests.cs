using System;
using System.IO;
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
				.EventPublisher()
				.Subscribe<MockSubscriber>(Guid.NewGuid());

			try
			{
				Assert.AreEqual(1, MockSubscriber.MockSubscribers.Count);
				var subscriber = MockSubscriber.MockSubscribers[0];

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
				.EventPublisher(2)
				.Subscribe<MockSubscriber>(Guid.NewGuid());


			try
			{
				Assert.AreEqual(1, MockSubscriber.MockSubscribers.Count);
				var subscriber = MockSubscriber.MockSubscribers[0];

				var id = Guid.NewGuid();
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 5 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 4 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 3 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 2 });
				(config as Configure).MessageReceiver.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 });
				subscriber.ReceivedEvent.WaitOne(1000);

				Assert.AreEqual(5, ((subscriber.Received[0] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(4, ((subscriber.Received[1] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(3, ((subscriber.Received[2] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(2, ((subscriber.Received[3] as StoredEvent).Event as MockEvent).Increment);
				Assert.AreEqual(1, ((subscriber.Received[4] as StoredEvent).Event as MockEvent).Increment);

				Assert.AreEqual(5, subscriber.Received.Count);
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
	}
}
