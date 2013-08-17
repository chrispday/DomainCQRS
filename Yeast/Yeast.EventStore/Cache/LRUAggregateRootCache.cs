using System;
using System.Collections.Generic;

using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public static class LRUAggregateRootCacheConfigure
	{
		public static int DefaultCacheSize = 10000;
		public static IConfigure LRUAggregateRootCache(this IConfigure configure) { return configure.LRUAggregateRootCache(DefaultCacheSize); }
		public static IConfigure LRUAggregateRootCache(this IConfigure configure, int capacity)
		{
			if (1 > capacity)
			{
				throw new ArgumentOutOfRangeException("capacity", capacity, "Capacity cannot be less than 1.");
			}

			var c = configure as Configure;
			c.AggregateRootCache = new LRUAggregateRootCache(capacity);
			return configure;
		}
	}

	public class LRUAggregateRootCache : LRUDictionary<Guid, AggregateRootAndVersion>, IAggregateRootCache
	{
		public LRUAggregateRootCache(int capacity) : base(capacity) { }
	}
}
