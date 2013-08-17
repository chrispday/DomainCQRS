using System;
using System.Collections.Generic;

using System.Text;

namespace Yeast.EventStore
{
	public static class NoAggregateRootCacheConfigure
	{
		public static IConfigure NoAggregateRootCache(this IConfigure configure)
		{
			var c = configure as Configure;
			c.AggregateRootCache = new NoAggregateRootCache();
			return configure;
		}
	}

	public class NoAggregateRootCache : IAggregateRootCache
	{
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
