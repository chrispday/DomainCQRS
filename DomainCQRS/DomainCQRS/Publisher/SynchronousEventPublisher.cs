using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	/// <summary>
	/// Configure DomainCQRS to use the <see cref="SynchronousEventPublisher"/>.
	/// Events are published synchronously as the are persisted, does not publish historical events.
	/// </summary>
	public static class SynchronousEventPublisherConfigure
	{
		/// <summary>
		/// Configure DomainCQRS to use the <see cref="SynchronousEventPublisher"/>.
		/// Events are published synchronously as the are persisted, does not publish historical events.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/>.</param>
		/// <returns>The <see cref="IConfigure"/>.</returns>
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

	/// <summary>
	/// Publishes event synchronously as the are persisted, does not publish historical events.
	/// </summary>
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
