using System;
using System.Collections.Generic;

namespace DomainCQRS
{
	/// <summary>
	/// Configures Domain CQRS to not cache Aggregate Roots
	/// </summary>
	public static class NoAggregateRootCacheConfigure
	{
		/// <summary>
		/// Configure Domain CQRS to not cache Aggregate Roots
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/>.</param>
		/// <returns>The <see cref="IConfigure"/></returns>
		public static IConfigure NoAggregateRootCache(this IConfigure configure)
		{
			configure.Registry
				.BuildInstancesOf<IAggregateRootCache>()
				.TheDefaultIsConcreteType<NoAggregateRootCache>();
			return configure;
		}
	}

	/// <summary>
	/// Don't cache Aggregate Roots
	/// </summary>
	public class NoAggregateRootCache : IAggregateRootCache
	{
		/// <summary>
		/// This event is never called.
		/// </summary>
		public event EventHandler<Common.KeyValueRemovedArgs<Guid, AggregateRootAndVersion>> Removed;

		public void Add(Guid key, AggregateRootAndVersion value) {}
		public bool ContainsKey(Guid key) { return false; }
		public ICollection<Guid> Keys { get { return new List<Guid>(); } }
		public bool Remove(Guid key) { throw new NotImplementedException(); }
		public ICollection<AggregateRootAndVersion> Values { get { return new List<AggregateRootAndVersion>(); } }
		public void Add(KeyValuePair<Guid, AggregateRootAndVersion> item) { }
		public void Clear() {}
		public bool Contains(KeyValuePair<Guid, AggregateRootAndVersion> item) { return false; }
		public int Count { get { return 0; } }
		public bool IsReadOnly { get { return true; } }
		public IEnumerator<KeyValuePair<Guid, AggregateRootAndVersion>> GetEnumerator() { return new List<KeyValuePair<Guid, AggregateRootAndVersion>>().GetEnumerator(); }
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return this.GetEnumerator(); }
		
		public bool TryGetValue(Guid key, out AggregateRootAndVersion value)
		{
			value = default(AggregateRootAndVersion);
			return false;
		}

		public AggregateRootAndVersion this[Guid key]
		{
			get { throw new NotImplementedException(); }
			set {}
		}

		public void CopyTo(KeyValuePair<Guid, AggregateRootAndVersion>[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public bool Remove(KeyValuePair<Guid, AggregateRootAndVersion> item)
		{
			throw new NotImplementedException();
		}
	}
}
