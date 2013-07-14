using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IMessageReceiver
	{
		IEventStore EventStore { get; set; }

		void Receive(object message);
		IMessageReceiver Register<Message, AggregateRoot>();
		IMessageReceiver Register<Message, AggregateRoot>(string aggregateRootIdsProperty, string aggregateRootApplyCommandMethod);
	}
}
