using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yeast.EventStore.Provider;
using System.Linq;
using Yeast.EventStore.Common;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class MessageReceiverTests
	{
		static IEventStore EventStore;

		[ClassInitialize]
		public static void ClassInit(TestContext ctx)
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			EventStore = new EventStore() { EventSerializer = new BinaryFormatterSerializer(), EventStoreProvider = new FileEventStoreProvider() { Directory = directory, Logger = new DebugLogger() }.EnsureExists() };
		}

		[ClassCleanup]
		public static void ClassCleanup()
		{
			(EventStore as FileEventStoreProvider).Dispose();
			Directory.Delete((EventStore.EventStoreProvider as FileEventStoreProvider).Directory, true);
		}

		[TestMethod]
		public void MessageReceiver_Receive()
		{
			var eventStore = new MockEventStore();
			var MessageReceiver = new MessageReceiver() { EventStore = eventStore, AggregateRootCache = new LRUAggregateRootCache(1000) }.Register<MockCommand, MockAggregateRoot>();
			var command = new MockCommand() { AggregateRootId = Guid.NewGuid(), Increment = 1 };
			MessageReceiver.Receive(command);
			Assert.AreEqual(1, eventStore.Saved.Count);
			Assert.AreEqual(command.AggregateRootId, eventStore.Saved[0].Item1);
			Assert.IsInstanceOfType(eventStore.Saved[0].Item3, typeof(MockEvent));
			Assert.AreEqual(command.Increment, ((MockEvent)eventStore.Saved[0].Item3).Increment);
		}

		[TestMethod]
		public void MessageReceiver_Receive_CustomNames_NotICommand()
		{
			var eventStore = new MockEventStore();
			var MessageReceiver = new MessageReceiver() { EventStore = eventStore, DefaultAggregateRootIdProperty = "Id", AggregateRootCache = new LRUAggregateRootCache(1000) }.Register<MockCommand2, MockAggregateRoot>();
			var command = new MockCommand2() { Id = Guid.NewGuid(), Increment = 0 };
			MessageReceiver.Receive(command);
			Assert.AreEqual(1, eventStore.Saved.Count);
			Assert.AreEqual(command.Id, eventStore.Saved[0].Item1);
			Assert.IsInstanceOfType(eventStore.Saved[0].Item3, typeof(MockEvent));
			Assert.AreEqual(command.Increment, ((MockEvent)eventStore.Saved[0].Item3).Increment);
		}

		[TestMethod]
		public void MessageReceiver_Receive2Commands()
		{
			var MessageReceiver = new MessageReceiver() { EventStore = EventStore, AggregateRootCache = new LRUAggregateRootCache(1000) }
				.Register<MockCommand, MockAggregateRoot>()
				.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

			var id = Guid.NewGuid();

			MessageReceiver
				.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 })
				.Receive(new MockCommand2() { Id = id, Increment = 2 });

			var storedEvents = EventStore.Load(id, null, null, null, null).ToList();
			Assert.AreEqual(2, storedEvents.Count);
			Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
			Assert.AreEqual(1, ((MockEvent)storedEvents[0].Event).Increment);
			Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
			Assert.AreEqual(2, ((MockEvent)storedEvents[1].Event).Increment);
		}

		[TestMethod]
		public void MessageReceiver_Receive3Aggregates3Commands()
		{
			var MessageReceiver = new MessageReceiver() { EventStore = EventStore, AggregateRootCache = new LRUAggregateRootCache(1000) }
				.Register<MockCommand, MockAggregateRoot>()
				.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

			var id = Guid.NewGuid();
			var id2 = Guid.NewGuid();
			var id3 = Guid.NewGuid();

			MessageReceiver
				.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 })
				.Receive(new MockCommand() { AggregateRootId = id2, Increment = 4 })
				.Receive(new MockCommand() { AggregateRootId = id3, Increment = 7 })
				.Receive(new MockCommand2() { Id = id, Increment = 2 })
				.Receive(new MockCommand2() { Id = id2, Increment = 5 })
				.Receive(new MockCommand2() { Id = id3, Increment = 8 })
				.Receive(new MockCommand() { AggregateRootId = id, Increment = 3 })
				.Receive(new MockCommand() { AggregateRootId = id2, Increment = 6 })
				.Receive(new MockCommand() { AggregateRootId = id3, Increment = 9 });

			var storedEvents = EventStore.Load(id, null, null, null, null).ToList();
			Assert.AreEqual(3, storedEvents.Count);
			Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
			Assert.AreEqual(1, ((MockEvent)storedEvents[0].Event).Increment);
			Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
			Assert.AreEqual(2, ((MockEvent)storedEvents[1].Event).Increment);
			Assert.IsInstanceOfType(storedEvents[2].Event, typeof(MockEvent));
			Assert.AreEqual(3, ((MockEvent)storedEvents[2].Event).Increment);

			storedEvents = EventStore.Load(id2, null, null, null, null).ToList();
			Assert.AreEqual(3, storedEvents.Count);
			Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
			Assert.AreEqual(4, ((MockEvent)storedEvents[0].Event).Increment);
			Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
			Assert.AreEqual(5, ((MockEvent)storedEvents[1].Event).Increment);
			Assert.IsInstanceOfType(storedEvents[2].Event, typeof(MockEvent));
			Assert.AreEqual(6, ((MockEvent)storedEvents[2].Event).Increment);

			storedEvents = EventStore.Load(id3, null, null, null, null).ToList();
			Assert.AreEqual(3, storedEvents.Count);
			Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
			Assert.AreEqual(7, ((MockEvent)storedEvents[0].Event).Increment);
			Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
			Assert.AreEqual(8, ((MockEvent)storedEvents[1].Event).Increment);
			Assert.IsInstanceOfType(storedEvents[2].Event, typeof(MockEvent));
			Assert.AreEqual(9, ((MockEvent)storedEvents[2].Event).Increment);
		}
	}
}
