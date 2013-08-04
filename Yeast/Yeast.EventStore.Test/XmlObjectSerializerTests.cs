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

namespace Yeast.EventStore.Test
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
			using (var provider = new FileEventStoreProvider() { Directory = Path.Combine(directory, Guid.NewGuid().ToString()), Logger = new DebugLogger() }.EnsureExists() as FileEventStoreProvider)
			{
				var eventStore = new EventStore() { EventSerializer = new XmlObjectSerializer() { Serializer = new DataContractSerializer(typeof(object), new Type[] { typeof(MockEvent) }) }, EventStoreProvider = provider };
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

				Debug.WriteLine( Directory.EnumerateFiles(provider.Directory).Sum(f => new FileInfo(f).Length));
			}
		}

		[TestMethod]
		public void XmlObjectSerializer_ProtoBufSerializer()
		{
			using (var provider = new FileEventStoreProvider() { Directory = Path.Combine(directory, Guid.NewGuid().ToString()), Logger = new DebugLogger() }.EnsureExists() as FileEventStoreProvider)
			{
				var typeModel = RuntimeTypeModel.Create();
				typeModel.Add(typeof(MockCommand), true);
				var serializer = new XmlProtoSerializer(typeModel, typeof(MockCommand));
				var eventStore = new EventStore() { EventSerializer = new XmlObjectSerializer() { Serializer = serializer }, EventStoreProvider = provider };
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

				Debug.WriteLine(Directory.EnumerateFiles(provider.Directory).Sum(f => new FileInfo(f).Length));
			}
		}
	}
}
