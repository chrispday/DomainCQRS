using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventPublisher : IDisposable
	{
		ILogger Logger { get; set; }
		int BatchSize { get; set; }
		IEventStore EventStore { get; set; }

		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId);
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, string subscriberReceiveMethodName);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, string subscriberReceiveMethodName);
	}
}
