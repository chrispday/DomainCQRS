using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IMessageReceiver
	{
		ILogger Logger { get; set; }
		IEventStore EventStore { get; set; }
		IAggregateRootCache AggregateRootCache { get; set; }

		IMessageReceiver Receive(object message);
		IMessageReceiver Register<Message, AggregateRoot>();
		IMessageReceiver Register<Message, AggregateRoot>(string aggregateRootIdsProperty, string aggregateRootApplyMethod);
		bool IsRegistered(Type messageType);
	}
}
