using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public interface IMessageReceiver
	{
		ILogger Logger { get; }
		IEventStore EventStore { get; }
		IAggregateRootCache AggregateRootCache { get; }
		string DefaultAggregateRootIdProperty { get; }
		string DefaultAggregateRootApplyMethod { get; }

		IMessageReceiver Receive(object message);
		IMessageReceiver Register<Message, AggregateRoot>();
		IMessageReceiver Register<Message, AggregateRoot>(string aggregateRootIdsProperty, string aggregateRootApplyMethod);
		bool IsRegistered(Type messageType);
	}
}
