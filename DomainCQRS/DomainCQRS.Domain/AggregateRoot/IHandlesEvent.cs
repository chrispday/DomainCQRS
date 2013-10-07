using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Domain
{
	public interface IHandlesEvent<E>
		where E : IEvent
	{
		void Apply(E @event);
	}
}
