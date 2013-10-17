using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DomainCQRS.Persister;
using System.Linq;
using System.Runtime.Serialization;
using ProtoBuf.ServiceModel;
using ProtoBuf.Meta;
using System.Diagnostics;
using DomainCQRS.Common;

namespace DomainCQRS.Test
{
	[TestClass]
	public class XmlObjectSerializerTests
	{
		static string directory;

		[ClassInitialize]
		public static void ClassInit(TestContext ctx)
		{
			directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		}

		[ClassCleanup]
		public static void ClassCleanup()
		{
			Directory.Delete(directory, true);
		}

		[TestMethod]
		public void XmlObjectSerializer_DataContractSerializer()
		{
			var config = Configure.With()
				.DebugLogger()
				.XmlObjectSerializer(new DataContractSerializer(typeof(object), new Type[] { typeof(MockEvent) }))
				.MemoryEventPersister()
				.LRUAggregateRootCache()
				.EventStore()
				.MessageReceiver()
				.Build()
					.Register<MockCommand, MockAggregateRoot>()
					.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

			using (config)
			{
				var id = Guid.NewGuid();

				config.MessageReceiver
					.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 })
					.Receive(new MockCommand2() { Id = id, Increment = 2 });

				var storedEvents = config.EventStore.Load(id, null, null, null, null).ToList();
				Assert.AreEqual(2, storedEvents.Count);
				Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
				Assert.AreEqual(1, ((MockEvent)storedEvents[0].Event).Increment);
				Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
				Assert.AreEqual(2, ((MockEvent)storedEvents[1].Event).Increment);
			}
		}

		[TestMethod]
		public void XmlObjectSerializer_ProtoBufSerializer()
		{
			var typeModel = RuntimeTypeModel.Create();
			typeModel.Add(typeof(MockCommand), true);
			var serializer = new XmlProtoSerializer(typeModel, typeof(MockCommand));

			var config = Configure.With()
				.DebugLogger()
				.XmlObjectSerializer(serializer)
				.MemoryEventPersister()
				.LRUAggregateRootCache()
				.EventStore()
				.MessageReceiver()
				.Build()
					.Register<MockCommand, MockAggregateRoot>()
					.Register<MockCommand2, MockAggregateRoot>("Id", "Apply");

			using (config)
			{
				var id = Guid.NewGuid();

				config.MessageReceiver
					.Receive(new MockCommand() { AggregateRootId = id, Increment = 1 })
					.Receive(new MockCommand2() { Id = id, Increment = 2 });

				var storedEvents = config.EventStore.Load(id, null, null, null, null).ToList();
				Assert.AreEqual(2, storedEvents.Count);
				Assert.IsInstanceOfType(storedEvents[0].Event, typeof(MockEvent));
				Assert.AreEqual(1, ((MockEvent)storedEvents[0].Event).Increment);
				Assert.IsInstanceOfType(storedEvents[1].Event, typeof(MockEvent));
				Assert.AreEqual(2, ((MockEvent)storedEvents[1].Event).Increment);
			}
		}
	}
}
