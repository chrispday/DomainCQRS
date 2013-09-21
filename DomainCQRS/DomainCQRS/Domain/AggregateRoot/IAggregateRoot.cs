using System;
using System.Collections;

namespace DomainCQRS.Domain
{
	public interface IAggregateRoot
	{
		Guid AggregateRootId { get; set; }
	}
}
