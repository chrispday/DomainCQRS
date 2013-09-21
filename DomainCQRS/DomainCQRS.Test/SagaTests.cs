﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DomainCQRS.Test.Mock;

namespace DomainCQRS.Test
{
	[TestClass]
	public class SagaTests
	{
		[TestMethod]
		public void SagaTest_Saga()
		{
			var config = Configure.With()
				.BinaryFormatterSerializer()
				.DebugLogger(true)
				.EventStore()
				.NoAggregateRootCache()
				.MemoryEventStoreProvider()
				.MessageReceiver()
					.Register<MockSagaCommand, MockSagaAggregateRoot>()
					.Register<MockSagaEvent, MockSaga>("SagaId")
				.MockEventPublisher(100, TimeSpan.FromSeconds(1))
				.SagaPublisher()
					.Saga<MockSagaEvent>();

			Guid aggregateRootId = Guid.NewGuid();
			config.GetMessageReceiver.Receive(new MockSagaCommand() { AggregateRootId = aggregateRootId, Message = "1" });
			config.GetMessageReceiver.Receive(new MockSagaCommand() { AggregateRootId = aggregateRootId, Message = "2" });

			System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));

			var events = config.GetMessageReceiver.EventStore.Load(aggregateRootId, null, null, null, null).ToList();
			Assert.AreEqual(2, events.Count);
			Assert.AreEqual("1", (events[0].Event as MockSagaEvent).Message);
			var sagaId = (events[0].Event as MockSagaEvent).SagaId;
			Assert.AreEqual("2", (events[1].Event as MockSagaEvent).Message);
			Assert.AreEqual(sagaId, (events[1].Event as MockSagaEvent).SagaId);

			events = config.GetMessageReceiver.EventStore.Load(sagaId, null, null, null, null).ToList();
			Assert.AreEqual(2, events.Count);
			Assert.AreEqual("Saga 1", (events[0].Event as MockSagaEvent2).Message);
			Assert.AreEqual("Saga 2", (events[1].Event as MockSagaEvent2).Message);

			config.GetEventPublisher.Dispose();
		}
	}
}