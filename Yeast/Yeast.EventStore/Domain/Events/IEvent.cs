using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface IEvent
	{
		Guid AggregateRootId { get; set; }
	}
}
