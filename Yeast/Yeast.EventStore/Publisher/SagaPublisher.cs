using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
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
			c.Subscribe<SagaPublisher>(SagaPublisherGuid, new SagaPublisher() { MessageReceiver = c.MessageReceiver });

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
		public IMessageReceiver MessageReceiver { get; set; }
		private HashSet<Type> _events = new HashSet<Type>();

		public ISagaPublisher Saga<Event>()
		{
			_events.Add(typeof(Event));
			return this;
		}

		public void Receive(object @event)
		{
			if (_events.Contains(@event.GetType()))
			{
				MessageReceiver.Receive(@event);
			}
		}
	}
}
