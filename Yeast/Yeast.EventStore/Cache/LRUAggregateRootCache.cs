using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public static class LRUAggregateRootCacheConfigure
	{
		public static IConfigure LRUAggregateRootCache(this IConfigure configure) { return configure.LRUAggregateRootCache(10000); }
		public static IConfigure LRUAggregateRootCache(this IConfigure configure, int capacity)
		{
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
