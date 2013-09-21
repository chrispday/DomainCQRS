using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public interface IEventProjector<Event>
	{
		void Receive(Event @event);
	}
}
