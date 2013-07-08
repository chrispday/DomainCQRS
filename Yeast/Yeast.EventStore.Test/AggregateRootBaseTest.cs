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
		[Serializable]
		public class TestCommand
		{
			public int Increment { get; set; }
		}

		[Serializable]
		public class TestCommand2
		{
			public int Increment { get; set; }
		}

		public class AggregateTest : AggregateRootBase<AggregateTest>, IHandles<TestCommand>
		{
			public int Amount { get; set; }

			public AggregateTest()
			{
				Amount = 0;
			}

			public AggregateTest(Guid id) : base(id) { }

			public void Handle(TestCommand command)
			{
				Amount += command.Increment;
			}
		}

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
		public void NewAggregate()
		{
			var at = new AggregateTest();

			Assert.AreNotEqual(Guid.Empty, at.AggregateId);
			Assert.AreEqual(-1, at.Version);
		}

		[TestMethod]
		public void LoadByConstructor()
		{
			var at = new AggregateTest();
			at.HandleCommand(new TestCommand() { Increment = 2 });

			Assert.AreEqual(2, at.Amount);

			var at2 = new AggregateTest(at.AggregateId);
			Assert.AreEqual(at.AggregateId, at2.AggregateId);
			Assert.AreEqual(2, at2.Amount);
			Assert.AreEqual(at.Version, at2.Version);
		}

		[TestMethod, ExpectedException(typeof(CommandHandlerException))]
		public void CommandWithoutHandler()
		{
			var at = new AggregateTest();
			at.HandleCommand(new TestCommand2());
		}
	}
}
