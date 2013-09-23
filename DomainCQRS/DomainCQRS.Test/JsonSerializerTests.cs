using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DomainCQRS.Provider;
using System.Linq;
using System.Runtime.Serialization;
using ProtoBuf.ServiceModel;
using ProtoBuf.Meta;
using System.Diagnostics;
using DomainCQRS.Common;
using DomainCQRS.Serialization;

namespace DomainCQRS.Test
{
	[TestClass]
	public class JsonSerializerTests
	{
		[TestMethod]
		public void JsonSerializer_SerializeDeserialize()
		{
			var config = Configure.With()
				.DebugLogger(true)
				.MemoryEventStoreProvider()
				.JsonSerializer()
				.LRUAggregateRootCache()
				.EventStore()
				.MessageReceiver()
				.Build()
					.Register<MockCommand, MockAggregateRoot>()
					.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

				var id = Guid.NewGuid();

				config.MessageReceiver
					.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 })
					.Receive(new MockCommand2() { Id = id, Increment = 2 });

				var storedEvents = config.MessageReceiver.EventStore.Load(id, null, null, null, null).ToList();
				Assert.AreEqual(2, storedEvents.Count);
				Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
				Assert.AreEqual(1, ((MockEvent)storedEvents[0].Event).Increment);
				Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
				Assert.AreEqual(2, ((MockEvent)storedEvents[1].Event).Increment);
		}
	}
}
