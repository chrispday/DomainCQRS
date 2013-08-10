using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface ISagaPublisher : IEventProjector<object>
	{
		IMessageReceiver MessageReceiver { get; set; }
		ISagaPublisher Saga<Event>();
	}
}
