using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test
{
	[TestClass]
	public class EventStoreTests
	{
		[TestMethod]
		public void EventUpgrading()
		{
			new Story("Event Upgrading")
				 .InOrderTo("support new versions of events")
				 .AsA("Programmer")
				 .IWant("event upgrading")

							.WithScenario("Event Upgrading")
								 .Given(EventUpgraderHasBeenRegistered)
									  .And(AnEventHasBeenSaved)
								 .When(TheEventIsLoaded)
								 .Then(ExpectTheUpgradedEvent)
				 .Execute();
		}

		private Configure config;
		private void EventUpgraderHasBeenRegistered()
		{
			config = Configure.With()
				.BinaryFormatterSerializer()
				.DebugLogger()
				.MemoryEventStoreProvider()
				.EventStore()
				.Build()
					.Upgrade<MockEvent, MockEvent2>()
				as Configure;
		}

		Guid id = Guid.NewGuid();
		MockEvent @event;
		private void AnEventHasBeenSaved()
		{
			@event = new MockEvent() { AggregateRootId = id, BatchNo = 1, Increment = 5 };
			config.EventStore.Save(id, 1, typeof(MockAggregateRoot), @event);
		}

		object loadedEvent;
		private void TheEventIsLoaded()
		{
			loadedEvent = config.EventStore.Load(id, null, null, null, null).First().Event;
		}

		private void ExpectTheUpgradedEvent()
		{
			Assert.IsInstanceOfType(loadedEvent, typeof(MockEvent2));
			Assert.AreEqual(id, (loadedEvent as MockEvent2).AggregateRootId);
			Assert.AreEqual(-5, (loadedEvent as MockEvent2).Increment);
		}
	}
}
