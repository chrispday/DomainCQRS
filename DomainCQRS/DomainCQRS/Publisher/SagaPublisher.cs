using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	public static class SagaPublisherConfigure
	{
		public static readonly Guid SagaPublisherGuid = new Guid("CCB8D62B-693E-45D1-B227-FBAFA548DE9B");

		public static IConfigure SagaPublisher(this IConfigure configure)
		{
			var c = configure as Configure;

			if (null == c.MessageReceiver)
			{
				throw new ArgumentNullException("MessageReceiver");
			}
			c.Subscribe<SagaPublisher>(SagaPublisherGuid, new SagaPublisher(
				c.MessageReceiver
				));

			return configure;
		}

		public static IConfigure Saga<Event>(this IConfigure configure)
		{
			var c = configure as Configure;
			c.EventPublisher.GetSubscriber<SagaPublisher>(SagaPublisherGuid).Saga<Event>();
			return configure;
		}
	}

	public class SagaPublisher : ISagaPublisher
	{
		private IMessageReceiver _messageReceiver;
		public IMessageReceiver MessageReceiver { get { return _messageReceiver; } }
		private Dictionary<Type, object> _events = new Dictionary<Type, object>();

		public SagaPublisher(IMessageReceiver messageReceiver)
		{
			if (null == messageReceiver)
			{
				throw new ArgumentNullException("messageReceiver");
			}

			_messageReceiver = messageReceiver;
		}

		public ISagaPublisher Saga<Event>()
		{
			_events.Add(typeof(Event), null);
			return this;
		}

		public void Receive(object @event)
		{
			if (_events.ContainsKey(@event.GetType()))
			{
				MessageReceiver.Receive(@event);
			}
		}
	}
}
