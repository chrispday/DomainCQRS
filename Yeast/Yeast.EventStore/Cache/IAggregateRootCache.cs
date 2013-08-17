using System;
using System.Collections.Generic;

using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IAggregateRootCache : IDictionary<Guid, AggregateRootAndVersion>
	{
		event EventHandler<KeyValueRemovedArgs<Guid, AggregateRootAndVersion>> Removed;
	}
}
