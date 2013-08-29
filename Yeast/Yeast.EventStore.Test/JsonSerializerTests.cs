using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yeast.EventStore.Provider;
using System.Linq;
using System.Runtime.Serialization;
using ProtoBuf.ServiceModel;
using ProtoBuf.Meta;
using System.Diagnostics;
using Yeast.EventStore.Common;
using Yeast.EventStore.Serialization;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class JsonSerializerTests
	{
		[TestMethod]
		public void JsonSerializer_SerializeDeserialize()
		{
			using (var provider = new MemoryEventStoreProvider() { Logger = new DebugLogger() }.EnsureExists())
			{
				var eventStore = new EventStore() { EventSerializer = new JsonSerializer(), EventStoreProvider = provider };
				var eventReceiver = new MessageReceiver() { EventStore = eventStore, AggregateRootCache = new LRUAggregateRootCache(1000), Logger = new DebugLogger() }
					.Register<MockCommand, MockAggregateRoot>()
					.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

				var id = Guid.NewGuid();

				eventReceiver
					.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 })
					.Receive(new MockCommand2() { Id = id, Increment = 2 });

				var storedEvents = eventStore.Load(id, null, null, null, null).ToList();
				Assert.AreEqual(2, storedEvents.Count);
				Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
				Assert.AreEqual(1, ((MockEvent)storedEvents[0].Event).Increment);
				Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
				Assert.AreEqual(2, ((MockEvent)storedEvents[1].Event).Increment);
			}
		}
	}
}
