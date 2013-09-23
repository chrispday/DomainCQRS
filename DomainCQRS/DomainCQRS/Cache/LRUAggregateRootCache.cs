using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class LRUAggregateRootCacheConfigure
	{
		public static int DefaultCacheSize = 10000;
		public static IConfigure LRUAggregateRootCache(this IConfigure configure) { return configure.LRUAggregateRootCache(DefaultCacheSize); }
		public static IConfigure LRUAggregateRootCache(this IConfigure configure, int capacity)
		{
			configure.Registry
				.BuildInstancesOf<IAggregateRootCache>()
				.TheDefaultIs(Registry.Instance<IAggregateRootCache>()
					.UsingConcreteType<LRUAggregateRootCache>()
					.WithProperty("capacity").EqualTo(capacity));
			return configure;
		}
	}

	public class LRUAggregateRootCache : LRUDictionary<Guid, AggregateRootAndVersion>, IAggregateRootCache
	{
		public LRUAggregateRootCache(int capacity) : base(capacity) { }
	}
}
