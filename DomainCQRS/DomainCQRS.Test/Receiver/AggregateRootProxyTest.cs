using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Receiver
{
	[TestClass]
	public class AggregateRootProxyTest
	{
		[TestMethod]
		public void AggregateRootProxies()
		{
			new Story("Aggregate Root Proxies")
				 .InOrderTo("make dealing with ARs easier")
				 .AsA("Programmer")
				 .IWant("a proxy object which will call methods")

							.WithScenario("Aggregate Root Creation")
								 .Given(AnARProxy)
								 .When(AnARIsCreated)
								 .Then(ItShouldCreateAnAR)

							.WithScenario("Aggregate Root Command that returns many events")
								 .Given(AnARProxy)
									  .And(ThatARHandlesACommandThatReturnsManyEvents)
								 .When(TheARProxyCallsTheCommandHandler, new CM())
								 .Then(ItShouldReturnTheManyEvents)

							.WithScenario("Aggregate Root Command that returns an event")
								 .Given(AnARProxy)
									  .And(ThatARHandlesACommandThatReturnsAnEvent)
								 .When(TheARProxyCallsTheCommandHandler, new C1())
								 .Then(ItShouldReturnAnEvent)

							.WithScenario("Aggregate Root Applies Event")
								 .Given(AnARProxy)
									  .And(ThatARAppliesAnEvent)
								 .When(TheARProxyCallsTheEventHandler)
								 .Then(ItShouldHandleTheEvent)
				 .Execute();
		}

		IAggregateRootProxy proxy;
		public class AR
		{
			public IEnumerable<object> Apply(CM c)
			{
				return new object[] { new E(), new E() };
			}

			public object Apply(C1 c)
			{
				return new E();
			}

			public List<E> es = new List<E>();
			public void Apply(E e)
			{
				es.Add(e);
			}
		}
		public class CM
		{
		}
		public class C1
		{
		}
		public class E
		{
		}
		private void AnARProxy()
		{
			proxy = typeof(AR).CreateAggregateRootProxy();
		}

		object created;
		private void AnARIsCreated()
		{
			created = proxy.Create();
		}

		private void ItShouldCreateAnAR()
		{
			Assert.IsInstanceOfType(created, typeof(AR));
		}

		private void ThatARHandlesACommandThatReturnsManyEvents()
		{
			proxy.Register(typeof(CM).CreateMessageProxy(), "Apply");
		}

		object e;
		private void TheARProxyCallsTheCommandHandler(object c)
		{
			e = proxy.ApplyCommand(new AR(), c);
		}

		private void ItShouldReturnTheManyEvents()
		{
			Assert.IsInstanceOfType(e, typeof(IEnumerable<object>));
			Assert.IsTrue(((IEnumerable<object>)e).All(ev => ev.GetType() == typeof(E)));
		}

		private void ThatARHandlesACommandThatReturnsAnEvent()
		{
			proxy.Register(typeof(C1).CreateMessageProxy(), "Apply");
		}

		private void ItShouldReturnAnEvent()
		{
			Assert.IsInstanceOfType(e, typeof(IEnumerable<object>));
			Assert.AreEqual(1, ((IEnumerable<object>)e).Count());
			Assert.IsInstanceOfType(((IEnumerable<object>)e).First(), typeof(E));
		}

		private void ThatARAppliesAnEvent()
		{
		}

		AR ar = new AR();
		private void TheARProxyCallsTheEventHandler()
		{
			proxy.ApplyEvent(ar, new E());
		}

		private void ItShouldHandleTheEvent()
		{
			Assert.AreEqual(1, ar.es.Count);
			Assert.IsInstanceOfType(ar.es.First(), typeof(E));
		}
	}
}
