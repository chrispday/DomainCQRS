using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DomainCQRS.Common;
using DomainCQRS.Provider;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test
{
	[TestClass]
	public class EventStoreTest
	{
		[TestMethod]
		public void EventStore()
		{
			new Story("Event Store")
				 .InOrderTo("store events")
				 .AsA("Programmer")
				 .IWant("to create an event store")

							.WithScenario("Save Event")
								 .Given(AnEventStore)
								 .When(AnEventIsSaved)
								 .Then(TheEventShouldBeAbleToBeLoaded)
									  .And(TheEventShouldHaveTheSameARId)
									  .And(TheEventShouldHaveTheARType)
									  .And(TheEventShouldHaveTheSameVersion)
									  .And(TheEventShouldHaveTheSameEventType)
									  .And(TheEventDataShouldBeTheSame)
									  .And(TheEventTimestampShouldBeTheSame)

							.WithScenario("Load Versions")
								 .Given(AnEventStore)
									  .And(SomeEventsAreStoredForTheSameARButDifferentVersions)
								 .When(WeLoadEventsBetween2Version)
								 .Then(OnlyEventsBetweenThose2VersionsAreLoaded)

							.WithScenario("Load Timestamps")
								 .Given(AnEventStore)
									  .And(SomeEventsAreStoredForTheSameARButDifferentTimestamps)
								 .When(WeLoadEventsBetween2Timestamps)
								 .Then(OnlyEventsBetweenThose2TimestampsAreLoaded)

				 .Execute();
		}

		IEventStore eventStore;
		private void AnEventStore()
		{
			var logger = new DebugLogger(true);
			eventStore = new EventStore(
				logger,
				new MemoryEventStoreProvider(logger).EnsureExists(),
				new BinaryFormatterSerializer(),
				8096);
		}

		Guid arId = Guid.NewGuid();
		DateTime now;
		DateTime then;
		private void AnEventIsSaved()
		{
			then = DateTime.Now;
			eventStore.Save(arId, 1, typeof(Guid), arId);
			now = DateTime.Now;
		}

		StoredEvent storedEvent;
		private void TheEventShouldBeAbleToBeLoaded()
		{
			storedEvent = eventStore.Load(arId, 1, 1, null, null).FirstOrDefault();
		}

		private void TheEventShouldHaveTheSameARId()
		{
			Assert.AreEqual(arId, storedEvent.AggregateRootId);
		}

		private void TheEventShouldHaveTheARType()
		{
			Assert.AreEqual(typeof(Guid), Type.GetType(storedEvent.AggregateRootType));
		}

		private void TheEventShouldHaveTheSameVersion()
		{
			Assert.AreEqual(1, storedEvent.Version);
		}

		private void TheEventShouldHaveTheSameEventType()
		{
			Assert.AreEqual(typeof(Guid), storedEvent.Event.GetType());
		}

		private void TheEventDataShouldBeTheSame()
		{
			Assert.AreEqual(arId, storedEvent.Event);
		}

		private void TheEventTimestampShouldBeTheSame()
		{
			Assert.IsTrue(then <= storedEvent.Timestamp && storedEvent.Timestamp <= now);
		}

		Guid sameARId = Guid.NewGuid();
		private void SomeEventsAreStoredForTheSameARButDifferentVersions()
		{
			eventStore.Save(sameARId, 1, typeof(Guid), 1);
			eventStore.Save(sameARId, 2, typeof(Guid), 2);
			eventStore.Save(sameARId, 3, typeof(Guid), 3);
			eventStore.Save(sameARId, 4, typeof(Guid), 4);
		}

		IEnumerable<StoredEvent> storedEvents;
		private void WeLoadEventsBetween2Version()
		{
			storedEvents = eventStore.Load(sameARId, 2, 3, null, null);
		}

		private void OnlyEventsBetweenThose2VersionsAreLoaded()
		{
			Assert.IsTrue(storedEvents.All(se => se.Version >= 2 && se.Version <= 3));
		}

		Guid sameARId2 = Guid.NewGuid();
		private void SomeEventsAreStoredForTheSameARButDifferentTimestamps()
		{
			eventStore.Save(sameARId2, 1, typeof(Guid), 1);
			Thread.Sleep(10);
			then = DateTime.Now;
			Thread.Sleep(10);
			eventStore.Save(sameARId2, 2, typeof(Guid), 2);
			Thread.Sleep(10);
			eventStore.Save(sameARId2, 3, typeof(Guid), 3);
			Thread.Sleep(10);
			now = DateTime.Now;
			Thread.Sleep(10);
			eventStore.Save(sameARId2, 4, typeof(Guid), 4);
		}

		private void WeLoadEventsBetween2Timestamps()
		{
			storedEvents = eventStore.Load(sameARId, null, null, then, now);
		}

		private void OnlyEventsBetweenThose2TimestampsAreLoaded()
		{
			Assert.IsTrue(storedEvents.All(se => se.Timestamp >= then && se.Timestamp <= now));
		}

		[TestMethod]
		public void EventUpgrading()
		{
			new Story("Event Upgrading")
				 .InOrderTo("use new versions of events")
				 .AsA("Programmer")
				 .IWant("to upgrade old versions of an event to the new one")

							.WithScenario("Save Event")
								 .Given(AnEventStore)
									  .And(TheOldVersionOfAnEventIsSaved)
								 .When(AnUpgradeToTheNewVersionOfTheEventIsRegistered)
								 .Then(TheEventShouldBeUpgradedToTheNewVersionOfTheEvent)
				 .Execute();
		}

		[Serializable]
		public class EventOld
		{
			public int I;
		}
		Guid upgradeAR = Guid.NewGuid();
		EventOld old;
		private void TheOldVersionOfAnEventIsSaved()
		{
			eventStore.Save(upgradeAR, 1, typeof(Guid), old = new EventOld() { I = 1 });
		}

		[Serializable]
		public class EventNew
		{
			public EventNew(EventOld old)
			{
				J = old.I;
			}

			public int J;
		}
		private void AnUpgradeToTheNewVersionOfTheEventIsRegistered()
		{
			eventStore.Upgrade<EventOld, EventNew>();
		}

		private void TheEventShouldBeUpgradedToTheNewVersionOfTheEvent()
		{
			var e = eventStore.Load(upgradeAR, 1, 1, null, null).FirstOrDefault();
			Assert.IsInstanceOfType(e.Event, typeof(EventNew));
			Assert.AreEqual(old.I, (e.Event as EventNew).J);
		}

		[TestMethod]
		public void EventPublishing()
		{
			new Story("Event Loading by Position")
				 .InOrderTo("known when new events are saved")
				 .AsA("Programmer")
				 .IWant("the event store to load new events based on position")

							.WithScenario("Load New Events")
								 .Given(AnEventStore)
								 .When(EventsAreSaved)
								 .Then(TheEventsShouldBeLoaded)

							.WithScenario("Load More New Events")
								 .Given(AnEventStore)
									  .And(SomeEventsThatHaveBeenLoaded)
								 .When(MoreEventsAreSaved)
								 .Then(ThoseEventsShouldBeLoaded)

							.WithScenario("Load Events From Beginning")
								 .Given(AnEventStore)
									  .And(SomeEventsThatHaveAlreadyBeenLoaded)
								 .When(LoadingFromTheStartPosition)
								 .Then(EventsFromTheBeginningShouldBeLoaded)

							.WithScenario("Load Events For Many ARs")
								 .Given(AnEventStore)
									  .And(SomeEventsSavedForManyArs)
								 .When(LoadingFromTheStartPosition)
								 .Then(EventsForAllArsShouldBeLoaded)

							.WithScenario("Load Events in Batches")
								 .Given(AnEventStore)
									  .And(EventsAreSaved)
								 .When(LoadingFromTheStartPositionInBatches)
								 .Then(EventsAreLoadedInBatches)
				 .Execute();
		}

		Guid loadAR = Guid.NewGuid();
		private void EventsAreSaved()
		{
			eventStore.Save(loadAR, 1, typeof(Guid), loadAR);
			eventStore.Save(loadAR, 2, typeof(Guid), loadAR);
		}

		private void TheEventsShouldBeLoaded()
		{
			var e = eventStore.Load(int.MaxValue, eventStore.CreateEventStoreProviderPosition(), eventStore.CreateEventStoreProviderPosition());
			Assert.AreEqual(2, e.Count());
			Assert.AreEqual(loadAR, e.First().AggregateRootId);
			Assert.AreEqual(1, e.First().Version);
			Assert.AreEqual(loadAR, e.Skip(1).First().AggregateRootId);
			Assert.AreEqual(2, e.Skip(1).First().Version);
		}

		Guid someAR = Guid.NewGuid();
		IEventStoreProviderPosition from;
		IEventStoreProviderPosition to;
		private void SomeEventsThatHaveBeenLoaded()
		{
			eventStore.Save(someAR, 1, typeof(Guid), someAR);
			eventStore.Save(someAR, 2, typeof(Guid), someAR);
			eventStore.Load(int.MaxValue, from = eventStore.CreateEventStoreProviderPosition(), to = eventStore.CreateEventStoreProviderPosition()).ToList();
		}

		private void MoreEventsAreSaved()
		{
			eventStore.Save(someAR, 3, typeof(Guid), someAR);
			eventStore.Save(someAR, 4, typeof(Guid), someAR);
		}

		private void ThoseEventsShouldBeLoaded()
		{
			var e = eventStore.Load(int.MaxValue, to, eventStore.CreateEventStoreProviderPosition());
			Assert.AreEqual(2, e.Count());
			Assert.AreEqual(someAR, e.First().AggregateRootId);
			Assert.AreEqual(3, e.First().Version);
			Assert.AreEqual(someAR, e.Skip(1).First().AggregateRootId);
			Assert.AreEqual(4, e.Skip(1).First().Version);
		}

		Guid subsAR = Guid.NewGuid();
		private void SomeEventsThatHaveAlreadyBeenLoaded()
		{
			eventStore.Save(subsAR, 1, typeof(Guid), subsAR);
			eventStore.Save(subsAR, 2, typeof(Guid), subsAR);
			eventStore.Load(int.MaxValue, eventStore.CreateEventStoreProviderPosition(), eventStore.CreateEventStoreProviderPosition());
		}

		IEnumerable<StoredEvent> newE;
		private void LoadingFromTheStartPosition()
		{
			newE = eventStore.Load(int.MaxValue, eventStore.CreateEventStoreProviderPosition(), eventStore.CreateEventStoreProviderPosition());
		}

		private void EventsFromTheBeginningShouldBeLoaded()
		{
			Assert.AreEqual(2, newE.Count());
			Assert.AreEqual(subsAR, newE.First().AggregateRootId);
			Assert.AreEqual(1, newE.First().Version);
			Assert.AreEqual(subsAR, newE.Skip(1).First().AggregateRootId);
			Assert.AreEqual(2, newE.Skip(1).First().Version);
		}

		Guid arOne = Guid.NewGuid();
		Guid arTwo = Guid.NewGuid();
		private void SomeEventsSavedForManyArs()
		{
			eventStore.Save(arOne, 1, typeof(Guid), arOne);
			eventStore.Save(arTwo, 1, typeof(Guid), arTwo);
		}

		private void EventsForAllArsShouldBeLoaded()
		{
			Assert.AreEqual(2, newE.Count());
			Assert.IsTrue(newE.Any(e => e.AggregateRootId == arOne && e.Version == 1));
			Assert.IsTrue(newE.Any(e => e.AggregateRootId == arTwo && e.Version == 1));
		}

		List<IEnumerable<StoredEvent>> batches = new List<IEnumerable<StoredEvent>>();
		private void LoadingFromTheStartPositionInBatches()
		{
			var from = eventStore.CreateEventStoreProviderPosition();
			var to = eventStore.CreateEventStoreProviderPosition();

			while (true)
			{
				batches.Add(eventStore.Load(1, from, to));
				if (0 == batches.Last().Count())
				{
					break;
				}
				from = to;
				to = eventStore.CreateEventStoreProviderPosition();
			}

			batches.Remove(batches.Last());
		}

		private void EventsAreLoadedInBatches()
		{
			Assert.IsTrue(batches.All(b => 1 == b.Count()));
		}
	}
}
