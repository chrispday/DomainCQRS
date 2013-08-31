using System;
using System.Collections.Generic;

using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IEventPublisher : IDisposable
	{
		ILogger Logger { get; set; }
		int BatchSize { get; set; }
		IEventStore EventStore { get; set; }
		IMessageReceiver MessageReceiver { get; set; }
		bool Synchronous { get; set; }

		void Publish(object @event);

		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId);
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, string subscriberReceiveMethodName);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, string subscriberReceiveMethodName);
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber);
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName);
		object GetSubscriber(Guid subscriptionId);
		Subscriber GetSubscriber<Subscriber>(Guid subscriptionId) where Subscriber : class;
	}
}
