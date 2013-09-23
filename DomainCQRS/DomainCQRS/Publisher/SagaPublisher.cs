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
			configure.Registry
				.BuildInstancesOf<ISagaPublisher>()
				.TheDefaultIsConcreteType<SagaPublisher>()
				.AsSingletons();
			return configure;
		}

		public static IBuiltConfigure Saga<Event>(this IBuiltConfigure configure)
		{
			configure.Subscribe<ISagaPublisher>(
				SagaPublisherGuid,
				configure.Container.CreateInstance<ISagaPublisher>()
					.Saga<Event>());
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
