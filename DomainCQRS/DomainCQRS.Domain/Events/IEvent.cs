using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Domain
{
	public interface IEvent
	{
		Guid AggregateRootId { get; set; }
	}
}
