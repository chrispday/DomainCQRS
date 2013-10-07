using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Cache
{
	[TestClass]
	public class NoAggregateRootCacheTest
	{
		[TestMethod]
		public void NoAggregateRootCache()
		{
			new Story("No Aggregate Root Cache")
				 .InOrderTo("not cache ARs")
				 .AsA("Programmer")
				 .IWant("to create a cache that never stores anything")

							.WithScenario("Doesnt Contain")
								 .Given(ANoARCache)
								 .When(AnARIsAdded)
								 .Then(ItShouldNotBeContainedInTheCache)
									  .And(TryingToGetTheValueShouldGetNothing)
									  .And(GettingAValueWillThrow)
									  .And(TheCacheIsEmpty)
									  .And(RemovingAValueWillThrow)
				 .Execute();
		}

		NoAggregateRootCache cache;
		private void ANoARCache()
		{
			if (null == cache)
			{
				cache = new NoAggregateRootCache();
			}
		}

		Guid key;
		private void AnARIsAdded()
		{
			key = Guid.NewGuid();
			cache.Add(key, new AggregateRootAndVersion() 
			{ 
				AggregateRootId = key, 
				LatestVersion = 1, 
				AggregateRoot = new object()
			});
		}

		private void ItShouldNotBeContainedInTheCache()
		{
			Assert.IsFalse(cache.ContainsKey(key));
		}

		private void TryingToGetTheValueShouldGetNothing()
		{
			AggregateRootAndVersion x;
			Assert.IsFalse(cache.TryGetValue(key, out x));
			Assert.IsNull(x);
		}

		private void GettingAValueWillThrow()
		{
			try
			{
				var x = cache[key];
				Assert.Fail();
			}
			catch { }
		}

		private void TheCacheIsEmpty()
		{
			Assert.AreEqual(0, cache.Count);
			Assert.AreEqual(0, cache.Keys.Count);
			Assert.AreEqual(0, cache.Values.Count);
			Assert.AreEqual(0, cache.AsEnumerable().Count());
		}

		private void RemovingAValueWillThrow()
		{
			try
			{
				var x = cache.Remove(key);
				Assert.Fail();
			}
			catch { }
		}
	}
}
