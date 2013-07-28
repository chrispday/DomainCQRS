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
	}
}
