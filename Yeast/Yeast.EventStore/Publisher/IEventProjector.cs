using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventProjector<Event>
	{
		void Receive(Event @event);
	}
}
