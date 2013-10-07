using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public interface IEventPublisher : IDisposable
	{
		ILogger Logger { get; }
		int BatchSize { get; }
		IEventStore EventStore { get; }
		IMessageReceiver MessageReceiver { get; set; }
		bool Synchronous { get; set; }

		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId);
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, string subscriberReceiveMethodName);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId) where Event : class;
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, string subscriberReceiveMethodName) where Event : class;
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber);
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName);
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber) where Event : class;
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName) where Event : class;
		object GetSubscriber(Guid subscriptionId);
		Subscriber GetSubscriber<Subscriber>(Guid subscriptionId) where Subscriber : class;
	}
}
