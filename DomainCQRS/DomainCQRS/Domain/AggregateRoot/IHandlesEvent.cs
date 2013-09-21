using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Domain
{
	public interface IHandlesEvent<E>
	{
		void Apply(E @event);
	}
}
