using System;
using System.Collections.Generic;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Interface for an Aggregate Root Cache
	/// </summary>
	public interface IAggregateRootCache : IDictionary<Guid, AggregateRootAndVersion>
	{
		/// <summary>
		/// Notifies when an Aggregate Root is removed from the cache.
		/// </summary>
		event EventHandler<KeyValueRemovedArgs<Guid, AggregateRootAndVersion>> Removed;
	}
}
