using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using DomainCQRS.Test.Mock;

namespace DomainCQRS.Test
{
	[TestClass]
	public class EventPublisherTests
	{
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
					.FileEventPersister(directory)
					.LRUAggregateRootCache(100)
					.EventStore()
					.MockSyncroEventPublisher()
					.MessageReceiver()
					.Build()
						.Subscribe<MockSubscriber>(subId)
						.Register<MockCommand, MockAggregateRoot>();

				var publisher = (config as Configure).EventPublisher as MockSynchroEventPublisher;
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
					.FileEventPersister(directory)
					.LRUAggregateRootCache(100)
					.EventStore()
					.MessageReceiver()
					.MockSyncroEventPublisher()
					.Build()
						.Register<MockCommand, MockAggregateRoot>()
						.Subscribe<MockSubscriber>(subId);

				publisher = (config as Configure).EventPublisher as MockSynchroEventPublisher;
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
