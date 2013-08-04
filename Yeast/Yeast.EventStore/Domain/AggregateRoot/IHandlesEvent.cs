using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface IHandlesEvent<E>
		where E : IEvent
	{
		void Apply(E @event);
	}
}
