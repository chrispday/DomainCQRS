using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	public interface ISagaPublisher : IEventProjector<object>
	{
		IMessageReceiver MessageReceiver { get; set; }
		ISagaPublisher Saga<Event>();
	}
}
