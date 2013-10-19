using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	public interface IEventProjector<Event>
	{
		Guid SubscriptionId { get; }
		void Receive(Event @event);
	}
}
