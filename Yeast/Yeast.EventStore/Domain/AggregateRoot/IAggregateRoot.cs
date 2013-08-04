using System;
using System.Collections;

namespace Yeast.EventStore.Domain
{
	public interface IAggregateRoot
	{
		Guid AggregateRootId { get; set; }
	}
}
