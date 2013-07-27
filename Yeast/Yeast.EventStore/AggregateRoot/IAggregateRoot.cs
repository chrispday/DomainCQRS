using System;
using System.Collections;

namespace Yeast.EventStore
{
	public interface IAggregateRoot
	{
		Guid AggregateRootId { get; set; }
	}
}
