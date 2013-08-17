using System;
using System.Collections.Generic;

using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface IEvent
	{
		Guid AggregateRootId { get; set; }
	}
}
