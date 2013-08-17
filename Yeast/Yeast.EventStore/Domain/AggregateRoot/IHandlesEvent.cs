using System;
using System.Collections.Generic;

using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface IHandlesEvent<E>
	{
		void Apply(E @event);
	}
}
