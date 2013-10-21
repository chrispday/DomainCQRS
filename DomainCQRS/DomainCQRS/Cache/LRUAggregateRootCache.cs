using System;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	/// <summary>
	/// Configures Domain CQRS to use the <see cref="LRUAggregateRootCache"/>.
	/// </summary>
	public static class LRUAggregateRootCacheConfigure
	{
		/// <summary>
		/// The default number of items to keep in the cache.
		/// </summary>
		public static int DefaultCacheSize = 10000;

		/// <summary>
		/// Configures Domain CQRS to use an <see cref="LRUAggregateRootCache"/>.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/>.</param>
		/// <returns>The <see cref="IConfigure"/></returns>
		public static IConfigure LRUAggregateRootCache(this IConfigure configure) { return configure.LRUAggregateRootCache(DefaultCacheSize); }
		/// <summary>
		/// Configures Domain CQRS to use an <see cref="LRUAggregateRootCache"/>.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/>.</param>
		/// <param name="capacity">The number of Aggregate Roots to keep in the cache.</param>
		/// <returns>The <see cref="IConfigure"/></returns>
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

	/// <summary>
	/// Cache's Aggregate Roots using a Least Recently Used strategy.
	/// </summary>
	public class LRUAggregateRootCache : LRUDictionary<Guid, AggregateRootAndVersion>, IAggregateRootCache
	{
		/// <summary>
		/// Create an <see cref="LRUAggregateRootCache"/>
		/// </summary>
		/// <param name="capacity">The number of Aggregate Roots to keep in the cache.</param>
		public LRUAggregateRootCache(int capacity) : base(capacity) { }
	}
}
