using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yeast.EventStore.Provider;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class AggregateRootBaseTest
	{
		static IEventStore EventStore;

		[ClassInitialize]
		public static void ClassInit(TestContext ctx)
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			EventStore = new EventStore() { Serializer = new BinaryFormatterSerializer(), EventStoreProvider = new FileEventStoreProvider() { Directory = directory }.EnsureExists() };
		}

		[ClassCleanup]
		public static void ClassCleanup()
		{
			(EventStore as FileEventStoreProvider).Dispose();
			Directory.Delete((EventStore.EventStoreProvider as FileEventStoreProvider).Directory, true);
		}

		[TestMethod]
		public void AggregateRootBase_NewAggregate()
		{
			var at = new MockAggregateRoot(EventStore);

			Assert.AreNotEqual(Guid.Empty, at.Id);
			Assert.AreEqual(-1, at.Version);
		}

		[TestMethod]
		public void AggregateRootBase_LoadByConstructor()
		{
			var at = new MockAggregateRoot(EventStore);
			at.Apply((object)new MockCommand() { Increment = 2 });

			Assert.AreEqual(2, at.Amount);

			var at2 = new MockAggregateRoot(EventStore, at.Id);
			Assert.AreEqual(at.Id, at2.Id);
			Assert.AreEqual(2, at2.Amount);
			Assert.AreEqual(at.Version, at2.Version);
		}

		[TestMethod, ExpectedException(typeof(CommandApplyException))]
		public void AggregateRootBase_CommandWithoutHandler()
		{
			var at = new MockAggregateRoot(EventStore);
			at.Apply(new MockCommand2());
		}
	}
}
