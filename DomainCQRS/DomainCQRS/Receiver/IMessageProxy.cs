using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	public interface IMessageProxy
	{
		Type Type { get; }
		IEnumerable<Guid> GetAggregateRootIds(Type aggregateRootType, object message);
		
		IMessageProxy Register(IAggregateRootProxy aggregateRootProxy, string aggregateRootIdsProperty);
		IEnumerable<IAggregateRootProxy> AggregateRootProxies { get; }
	}
}
