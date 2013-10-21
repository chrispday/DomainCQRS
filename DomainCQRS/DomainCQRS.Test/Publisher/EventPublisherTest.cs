using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DomainCQRS.Common;
using DomainCQRS.Persister;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Publisher
{
	[TestClass]
	public class EventPublisherTest
	{
		[TestMethod]
		public void EventPublishing()
		{
			new Story("Event Publishing")
				 .InOrderTo("known when new events are saved")
				 .AsA("Programmer")
				 .IWant("the event store to publish new events")

							.WithScenario("Publish New Events")
								 .Given(AnEventStoreAndPublisher)
								 .When(EventsAreSaved)
								 .Then(TheEventsShouldBePublished)

							.WithScenario("Publish More New Events")
								 .Given(AnEventStoreAndPublisher)
									  .And(SomeEventsThatHaveBeenPublished)
								 .When(MoreEventsAreSaved)
								 .Then(ThoseEventsShouldBePublished)

							.WithScenario("Publish Events From Beginning")
								 .Given(AnEventStoreAndPublisher)
									  .And(SomeEventsThatHaveBeenPublishedForASubscriber)
								 .When(ANewSubscriberIsAdded)
								 .Then(EventsFromTheBeginningShouldBePublished)

							.WithScenario("Retrieve Subscriber")
								 .Given(AnEventStoreAndPublisher)
								 .When(AskingForASubscriber)
								 .Then(TheSubscriberShouldBeReturned)
				 .Execute();

			eventPublisher.Dispose();
		}

		IEventStore eventStore;
		IEventPublisher eventPublisher;
		Guid subId = Guid.NewGuid();
		public class Subscriber
		{
			public List<object> es = new List<object>(); 

			public void Receive(object e)
			{
				es.Add(e);
			}
		}
		Subscriber sub = new Subscriber();
		private void AnEventStoreAndPublisher()
		{
			var logger = new DebugLogger(true);
			eventStore = new EventStore(
				logger,
				new MemoryEventPersister(logger).EnsureExists(),
				new BinaryFormatterSerializer(),
				8096);

			eventPublisher = new BatchEventPublisher(
				logger,
				eventStore,
				"Receive",
				100,
				TimeSpan.FromSeconds(0.1).Ticks);
			eventPublisher.Subscribe<Subscriber>(subId, sub);
		}

		Guid arId = Guid.NewGuid();
		private void EventsAreSaved()
		{
			eventStore.Save(arId, 1, typeof(Guid), arId);
			Thread.Sleep(200);
		}

		private void TheEventsShouldBePublished()
		{
			Assert.AreEqual(1, sub.es.Count);
			Assert.AreEqual(arId, sub.es.First());
		}

		private void SomeEventsThatHaveBeenPublished()
		{
			sub.es.Clear();
			eventStore.Save(arId, 1, typeof(Guid), arId);
			Thread.Sleep(200);
			Assert.AreEqual(1, sub.es.Count);
			Assert.AreEqual(arId, sub.es.First());
		}

		Guid more1Id = Guid.NewGuid();
		Guid more2Id = Guid.NewGuid();
		private void MoreEventsAreSaved()
		{
			eventStore.Save(arId, 2, typeof(Guid), more1Id);
			eventStore.Save(arId, 3, typeof(Guid), more2Id);
			Thread.Sleep(200);
		}

		private void ThoseEventsShouldBePublished()
		{
			Assert.AreEqual(3, sub.es.Count);
			Assert.AreEqual(more1Id, sub.es.Skip(1).First());
			Assert.AreEqual(more2Id, sub.es.Skip(2).First());
		}

		private void SomeEventsThatHaveBeenPublishedForASubscriber()
		{
			sub.es.Clear();
			eventStore.Save(arId, 1, typeof(Guid), arId);
			eventStore.Save(arId, 2, typeof(Guid), more1Id);
			Thread.Sleep(200);
			Assert.AreEqual(2, sub.es.Count);
			Assert.AreEqual(arId, sub.es.First());
			Assert.AreEqual(more1Id, sub.es.Skip(1).First());
		}

		Subscriber newSub = new Subscriber();
		Guid newSubId = Guid.NewGuid();
		private void ANewSubscriberIsAdded()
		{
			eventPublisher.Subscribe<Subscriber>(newSubId, newSub);
			Thread.Sleep(200);
		}

		private void EventsFromTheBeginningShouldBePublished()
		{
			Assert.AreEqual(2, newSub.es.Count);
			Assert.AreEqual(arId, newSub.es.First());
			Assert.AreEqual(more1Id, newSub.es.Skip(1).First());
		}

		Subscriber askSub;
		private void AskingForASubscriber()
		{
			askSub = eventPublisher.GetSubscriber<Subscriber>(subId);
		}

		private void TheSubscriberShouldBeReturned()
		{
			Assert.ReferenceEquals(askSub, sub);
		}

		[TestMethod]
		public void SynchronousEventPublishing()
		{
			new Story("Synchronous Event Publishing")
				 .InOrderTo("known when new events are saved straight away")
				 .AsA("Programmer")
				 .IWant("the event store to publish new events synchronously")

							.WithScenario("Publish New Events Synchronously")
								 .Given(ASynchronousEventStore)
								 .When(EventsAreSaved)
								 .Then(TheEventsShouldBePublished)

							.WithScenario("Publish More New Events Synchronously")
								 .Given(ASynchronousEventStore)
									  .And(SomeEventsThatHaveBeenPublished)
								 .When(MoreEventsAreSaved)
								 .Then(ThoseEventsShouldBePublished)

							.WithScenario("Dont Publish Events From Beginning")
								 .Given(ASynchronousEventStore)
									  .And(SomeEventsThatHaveBeenPublishedForASubscriber)
								 .When(ANewSyncSubscriberIsAdded)
									.And(AnEventIsSaved)
								 .Then(OnlyNewEventsShouldBePublished)
				 .Execute();
		}

		IEventPublisher syncEventPublisher;
		IMessageReceiver messageReceiver;
		private void ASynchronousEventStore()
		{
			var logger = new DebugLogger(true);
			eventStore = new EventStore(
				logger,
				new MemoryEventPersister(logger).EnsureExists(),
				new BinaryFormatterSerializer(),
				8096);

			syncEventPublisher = new SynchronousEventPublisher(
				logger,
				eventStore,
				"Receive");

			messageReceiver = new MessageReceiver(
				logger,
				eventStore,
				new NoAggregateRootCache(),
				"AggregateRootId",
				"Apply");

			syncEventPublisher.Subscribe<Subscriber>(subId, sub);
		}

		Guid newId = Guid.NewGuid();
		private void AnEventIsSaved()
		{
			eventStore.Save(newId, 1, typeof(Guid), newId);
		}

		private void OnlyNewEventsShouldBePublished()
		{
			Assert.AreEqual(1, newSub.es.Count);
			Assert.AreEqual(newId, newSub.es.First());
		}

		private void ANewSyncSubscriberIsAdded()
		{
			syncEventPublisher.Subscribe<Subscriber>(newSubId, newSub);
			Thread.Sleep(200);
		}
	}
}
