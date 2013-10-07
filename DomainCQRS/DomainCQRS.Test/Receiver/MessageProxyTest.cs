using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Receiver
{
	[TestClass]
	public class MessageProxyTest
	{
		[TestMethod]
		public void MessageProxies()
		{
			new Story("Message Proxies")
				 .InOrderTo("make dealing with messages easier")
				 .AsA("Programmer")
				 .IWant("a proxy object which will call methods")

							.WithScenario("Get Aggregate Root Id From Message")
								 .Given(AnAggregateProxy)
									  .And(AMessageProxyThatContainsAnARId)
								 .When(WeGetTheARId)
								 .Then(ItShouldReturnTheARId)

							.WithScenario("Get Aggregate Root Ids From Message")
								 .Given(AnAggregateProxy)
									  .And(AMessageProxyThatContainsARIds)
								 .When(WeGetTheARIds)
								 .Then(ItShouldReturnTheARIds)

							.WithScenario("Get Aggregate Roots")
								 .Given(TwoAggregateRootProxies)
									  .And(AMessageProxyThatHasIdsForBothARs)
								 .When(WeGetARsForTheMessage)
								 .Then(ItShouldReturnTheTwoARTypes)
				 .Execute();
		}

		public class AR1
		{
		}
		IAggregateRootProxy arProxy1;
		public class AR2
		{
		}
		IAggregateRootProxy arProxy2;
		private void AnAggregateProxy()
		{
			arProxy1 = typeof(AR1).CreateAggregateRootProxy();
			arProxy2 = typeof(AR2).CreateAggregateRootProxy();
		}

		public class M
		{
			public Guid ARId { get; set; }
			public IEnumerable<Guid> ARIds { get; set; }
		}
		IMessageProxy mProxy;
		private void AMessageProxyThatContainsAnARId()
		{
			mProxy = typeof(M).CreateMessageProxy();
			mProxy.Register(arProxy1, "ARId");
		}

		Guid arId1 = Guid.NewGuid();
		Guid arId2 = Guid.NewGuid();
		IEnumerable<Guid> arIds;
		private void WeGetTheARId()
		{
			arIds = mProxy.GetAggregateRootIds(typeof(AR1), new M() { ARId = arId1 });
		}

		private void ItShouldReturnTheARId()
		{
			Assert.AreEqual(1, arIds.Count());
			Assert.AreEqual(arId1, arIds.First());
		}

		private void AMessageProxyThatContainsARIds()
		{
			mProxy.Register(arProxy2, "ARIds");
		}

		private void WeGetTheARIds()
		{
			arIds = mProxy.GetAggregateRootIds(typeof(AR2), new M() { ARIds = new Guid[] { arId1, arId2 } });
		}

		private void ItShouldReturnTheARIds()
		{
			Assert.AreEqual(2, arIds.Count());
			Assert.AreEqual(arId1, arIds.First());
			Assert.AreEqual(arId2, arIds.Skip(1).First());
		}

		private void TwoAggregateRootProxies()
		{
		}

		private void AMessageProxyThatHasIdsForBothARs()
		{
		}

		IEnumerable<IAggregateRootProxy> arsForM;
		private void WeGetARsForTheMessage()
		{
			arsForM = mProxy.AggregateRootProxies;
		}

		private void ItShouldReturnTheTwoARTypes()
		{
			Assert.AreEqual(2, arsForM.Count());
			Assert.ReferenceEquals(arProxy1, arsForM.First());
			Assert.ReferenceEquals(arProxy2, arIds.Skip(1).First());
		}
	}
}
