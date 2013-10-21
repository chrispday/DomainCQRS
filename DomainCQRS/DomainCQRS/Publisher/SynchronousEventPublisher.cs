using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class SynchronousEventPublisherConfigure
	{
		public static IConfigure SynchronousEventPublisher(this IConfigure configure)
		{
			configure.Registry
				.BuildInstancesOf<IEventPublisher>()
				.TheDefaultIs(Registry.Instance<IEventPublisher>()
					.UsingConcreteType<SynchronousEventPublisher>()
					.WithProperty("defaultSubscriberReceiveMethodName").EqualTo(EventPublisherConfigure.DefaultSubscriberReceiveMethodName))
				.AsSingletons();
			return configure;
		}
	}

	public class SynchronousEventPublisher : EventPublisherBase, IEventPublisher
	{
		public SynchronousEventPublisher(ILogger logger, IEventStore eventStore, string defaultSubscriberReceiveMethodName)
			: base(logger, eventStore, defaultSubscriberReceiveMethodName)
		{
			eventStore.EventStored += eventStore_EventStored;
		}

		void eventStore_EventStored(object sender, StoredEvent e)
		{
			Publish(e.Event);
		}

		private void Publish(object @event)
		{
			var subscribers = new List<SubscriberAndPosition>(_subscribers.Values);
			foreach (var subscriber in subscribers)
			{
				Receive receive = subscriber.ReceiveObject;
				if (null == receive)
				{
					subscriber.Receives.TryGetValue(@event.GetType(), out receive);
				}

				if (null != receive)
				{
					receive(subscriber.Subscriber, @event);
				}
			}
		}

		public override void Dispose()
		{
		}
	}
}
