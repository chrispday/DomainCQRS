using System;
using System.Collections.Generic;
using System.Linq;
using DomainCQRS.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Cache
{
	[TestClass]
	public class LRUAggregateRootCacheTest
	{
		[TestMethod]
		public void LRUAggregateRootCache()
		{
			new Story("LRU Aggregate Root Cache")
				 .InOrderTo("cache a limited number of ARs, removing those least recently used")
				 .AsA("Programmer")
				 .IWant("to create a cache that caches ARs and removes those LRU")

							.WithScenario("Contains")
								 .Given(ALRUCache)
								 .When(AnARIsAdded)
								 .Then(ItBeContainedInTheCache)
									  .And(TryingToGetTheValueShouldGetTheAR)

							.WithScenario("Limited number")
								 .Given(ALRUCache)
								 .When(MoreARsAreAddedThanTheCapacity)
								 .Then(TheCountShouldBeEqualToOrLessThanTheCapacity)
									  .And(AnEventShouldBeTriggeredForEachARRemoved)

							.WithScenario("Remove LRU")
								 .Given(ALRUCache)
								 .When(SomeARsAreAddedLessThanTheCapacity)
									  .And(AnAlreadyAddedARIsAccessed)
									  .And(MoreARsAreAddedToExceedTheCapacity)
								 .Then(TheAccessedARShouldNotBeRemoved)
				 .Execute();
		}

		int capacity = 3;
		LRUAggregateRootCache cache;
		List<KeyValueRemovedArgs<Guid, AggregateRootAndVersion>> removed;
		List<Guid> added;
		private void ALRUCache()
		{
			cache = new LRUAggregateRootCache(capacity);
			cache.Removed += cache_Removed;
			added = new List<Guid>();
			removed = new List<KeyValueRemovedArgs<Guid, AggregateRootAndVersion>>();
		}

		void cache_Removed(object sender, KeyValueRemovedArgs<Guid, AggregateRootAndVersion> e)
		{
			removed.Add(e);
		}

		Guid key;
		AggregateRootAndVersion ar;
		private void AnARIsAdded()
		{
			key = Guid.NewGuid();
			cache.Add(key, ar = new AggregateRootAndVersion()
			{
				AggregateRoot = new object(),
				AggregateRootId = key,
				LatestVersion = 1
			});
		}

		private void ItBeContainedInTheCache()
		{
			Assert.IsTrue(cache.ContainsKey(key));
		}

		private void TryingToGetTheValueShouldGetTheAR()
		{
			AggregateRootAndVersion x;
			Assert.IsTrue(cache.TryGetValue(key, out x));
			Assert.AreSame(ar, x);
		}

		private void MoreARsAreAddedThanTheCapacity()
		{
			Add(Guid.NewGuid());
			Add(Guid.NewGuid());
			Add(Guid.NewGuid());
			Add(Guid.NewGuid());
			Add(Guid.NewGuid());
		}

		private void Add(Guid guid)
		{
			cache.Add(guid, new AggregateRootAndVersion()
			{
				AggregateRoot = new object(),
				AggregateRootId = guid,
				LatestVersion = 1
			});
			added.Add(guid);
		}

		private void TheCountShouldBeEqualToOrLessThanTheCapacity()
		{
			Assert.IsTrue(capacity >= cache.Count);
		}

		private void AnEventShouldBeTriggeredForEachARRemoved()
		{
			foreach (Guid key in added)
			{
				if (!cache.ContainsKey(key))
				{
					Assert.IsTrue(removed.Any(r => r.Key == key));
				}
			}
		}

		Guid accessAgain;
		private void SomeARsAreAddedLessThanTheCapacity()
		{
			accessAgain = Guid.NewGuid();
			Add(accessAgain);
			Add(Guid.NewGuid());
			Add(Guid.NewGuid());
		}

		private void AnAlreadyAddedARIsAccessed()
		{
			AggregateRootAndVersion x;
			cache.TryGetValue(accessAgain, out x);
		}

		private void MoreARsAreAddedToExceedTheCapacity()
		{
			Add(Guid.NewGuid());
		}

		private void TheAccessedARShouldNotBeRemoved()
		{
			Assert.AreNotEqual(0, removed.Count);
			Assert.IsTrue(!removed.Any(r => r.Key == accessAgain));
		}
	}
}
