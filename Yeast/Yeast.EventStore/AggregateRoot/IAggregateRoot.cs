using System;
using System.Collections;

namespace Yeast.EventStore
{
	public interface IAggregateRoot
	{
		Guid AggregateRootId { get; set; }
		int Version { get; set; }

		IEnumerable Apply(object command);
		object When(object @event);
	}
}
