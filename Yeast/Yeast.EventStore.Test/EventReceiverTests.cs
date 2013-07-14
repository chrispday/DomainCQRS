using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class EventReceiverTests
	{
		[TestMethod]
		public void EventReceiver_Receive()
		{
			var eventStore = new MockEventStore();
			var eventReceiver = new MessageReceiver() { EventStore = eventStore }.Register<MockCommand, MockAggregateRoot>();
			var command = new MockCommand() { AggregateRootId = Guid.NewGuid(), Version = 1, Increment = 0 };
			eventReceiver.Receive(command);
			Assert.AreEqual(1, eventStore.Saved.Count);
			Assert.AreEqual(command.AggregateRootId, eventStore.Saved[0].Item1);
			Assert.AreEqual(command.Version, eventStore.Saved[0].Item2);
			Assert.AreSame(command, eventStore.Saved[0].Item3);
		}

		[TestMethod]
		public void EventReceiver_Receive_CustomNames_NotICommand()
		{
			var eventStore = new MockEventStore();
			var eventReceiver = new MessageReceiver() { EventStore = eventStore, DefaultAggregateRootIdProperty = "Id" };
			var command = new MockCommand2() { AggregateRootId = Guid.NewGuid(), Ver = 1, Increment = 0 };
			eventReceiver.Receive(command);
			Assert.AreEqual(1, eventStore.Saved.Count);
			Assert.AreEqual(command.AggregateRootId, eventStore.Saved[0].Item1);
			Assert.AreEqual(command.Ver, eventStore.Saved[0].Item2);
			Assert.AreSame(command, eventStore.Saved[0].Item3);
		}
	}
}
