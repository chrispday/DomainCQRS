using System;
using System.Linq;
using DomainCQRS.Common;
using DomainCQRS.Persister;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Publisher
{
	[TestClass]
	public class SagaPublisherTest
	{
		[TestMethod]
		public void Sagas()
		{
			new Story("Sagas")
				 .InOrderTo("allow other Aggregate Roots to subscribe to events")
				 .AsA("Programmer")
				 .IWant("events published to be sent to the message receiver where they can be processed by the AR")

							.WithScenario("Aggregate Root subscribes to Event")
								 .Given(AnEventStore)
									  .And(ThereIsAMessageReceiver)
									  .And(ASynchronousEventPublisher)
									  .And(ItsSetupToPublishSagas)
									  .And(ThereIsAnARRegisteredToReceiveAnEventAsACommand)
								 .When(ThatEventIsSaved)
								 .Then(TheARShouldBeLoadedAndTheEventReceivedAsACommand)
				 .Execute();
		}

		ILogger logger;
		IEventStore eventStore;
		private void AnEventStore()
		{
			logger = new DebugLogger(true);
			eventStore = new EventStore(
	logger,
	new MemoryEventPersister(logger).EnsureExists(),
	new BinaryFormatterSerializer(),
	8096);
		}

		IEventPublisher eventPublisher;
		private void ASynchronousEventPublisher()
		{
			eventPublisher = new SynchronousEventPublisher(
				logger,
				eventStore,
				"Receive");
		}

		IMessageReceiver receiver;
		private void ThereIsAMessageReceiver()
		{
			receiver = new MessageReceiver(
				logger,
				eventStore,
				new NoAggregateRootCache(),
				"AggregateRootId",
				"Apply"
				);
		}

		[Serializable]
		public class E
		{
			public Guid AggregateRootId { get; set; }
		}
		ISagaPublisher sagaPublisher;
		private void ItsSetupToPublishSagas()
		{
			sagaPublisher = new SagaPublisher(new DirectMessageSender(logger, receiver));
			eventPublisher.Subscribe<ISagaPublisher>(Guid.NewGuid(), sagaPublisher);
			sagaPublisher.Saga<E>();
		}

		public class AR
		{
			public object Apply(E e)
			{
				return new SE() { AggregateRootId = e.AggregateRootId };
			}
		}
		[Serializable]
		public class SE
		{
			public Guid AggregateRootId { get; set; }
		}
		private void ThereIsAnARRegisteredToReceiveAnEventAsACommand()
		{
			receiver.Register<E, AR>();
		}

		Guid arId = Guid.NewGuid();
		Guid sagaId = Guid.NewGuid();
		private void ThatEventIsSaved()
		{
			eventStore.Save(arId, 1, typeof(Guid), new E() { AggregateRootId = sagaId });
		}

		private void TheARShouldBeLoadedAndTheEventReceivedAsACommand()
		{
			var es = eventStore.Load(sagaId, null, null, null, null);
			Assert.AreNotEqual(0, es.Count());
			Assert.IsTrue(es.All(e => e.Event.GetType() == typeof(SE)));
		}
	}
}
