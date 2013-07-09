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
		[TestInitialize]
		public void Init()
		{
			var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			AggregateRootBase.EventStore = EventStore.Current = new EventStore() { Serializer = new BinaryFormatterSerializer(), EventStoreProvider = new FileEventStoreProvider() { Directory = directory }.EnsureExists() };
		}

		[TestCleanup]
		public void Cleanup()
		{
			(EventStore.Current.EventStoreProvider as FileEventStoreProvider).Dispose();
			Directory.Delete((EventStore.Current.EventStoreProvider as FileEventStoreProvider).Directory, true);
		}

		[TestMethod]
		public void AggregateRootBase_NewAggregate()
		{
			var at = new MockAggregateRoot();

			Assert.AreNotEqual(Guid.Empty, at.AggregateId);
			Assert.AreEqual(-1, at.Version);
		}

		[TestMethod]
		public void AggregateRootBase_LoadByConstructor()
		{
			var at = new MockAggregateRoot();
			at.HandleCommand(new MockCommand() { Increment = 2 });

			Assert.AreEqual(2, at.Amount);

			var at2 = new MockAggregateRoot(at.AggregateId);
			Assert.AreEqual(at.AggregateId, at2.AggregateId);
			Assert.AreEqual(2, at2.Amount);
			Assert.AreEqual(at.Version, at2.Version);
		}

		[TestMethod, ExpectedException(typeof(CommandHandlerException))]
		public void AggregateRootBase_CommandWithoutHandler()
		{
			var at = new MockAggregateRoot();
			at.HandleCommand(new MockCommand2());
		}
	}
}
