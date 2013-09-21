using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public interface IAggregateRootCache : IDictionary<Guid, AggregateRootAndVersion>
	{
		event EventHandler<KeyValueRemovedArgs<Guid, AggregateRootAndVersion>> Removed;
	}
}
