using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace DomainCQRS.Domain
{
	public abstract class AggregateRootBase : IAggregateRoot
	{
		public Guid AggregateRootId { get; set; }

		public AggregateRootBase()
		{
			AggregateRootId = Guid.NewGuid();
		}

		public AggregateRootBase(Guid aggregateRootId)
		{
			AggregateRootId = aggregateRootId;
		}
	}
}
