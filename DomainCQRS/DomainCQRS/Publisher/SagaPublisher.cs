using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Configures DomainCQRS to use saga publishing.
	/// </summary>
	public static class SagaPublisherConfigure
	{
		public static readonly Guid SagaPublisherGuid = new Guid("CCB8D62B-693E-45D1-B227-FBAFA548DE9B");

		/// <summary>
		/// Configures DomainCQRS to use saga publishing.
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/>.</param>
		/// <returns>The <see cref="IConfigure"/>.</returns>
		public static IConfigure SagaPublisher(this IConfigure configure)
		{
			configure.Registry
				.BuildInstancesOf<ISagaPublisher>()
				.TheDefaultIsConcreteType<SagaPublisher>()
				.AsSingletons();
			return configure;
		}

		/// <summary>
		/// Registers an event to be treated as a command.
		/// </summary>
		/// <typeparam name="Event">The event type.</typeparam>
		/// <param name="configure">The <see cref="IBuiltConfigure"/>.</param>
		/// <returns>The <see cref="IBuiltConfigure"/>.</returns>
		public static IBuiltConfigure Saga<Event>(this IBuiltConfigure configure)
		{
			configure.Subscribe<ISagaPublisher>(
				SagaPublisherGuid,
				configure.Container.CreateInstance<ISagaPublisher>()
					.Saga<Event>());
			return configure;
		}
	}

	/// <summary>
	/// Publishes events to the message receiver to be consumed by Aggregate Roots as commands.
	/// </summary>
	public class SagaPublisher : ISagaPublisher
	{
		private IMessageSender _sender;
		/// <summary>
		/// The <see cref="IMessageSender"/> to use to send the events to the <see cref="IMessageReceiver"/>.
		/// </summary>
		public IMessageSender Sender { get { return _sender; } }
		private Dictionary<Type, object> _events = new Dictionary<Type, object>();
		/// <summary>
		/// The subscripion id to use when registering with the publisher.
		/// Should be implemented as a static readonly Guid so the same id is used every time.
		/// </summary>
		public Guid SubscriptionId
		{
			get { return SagaPublisherConfigure.SagaPublisherGuid; }
		}

		public SagaPublisher(IMessageSender sender)
		{
			if (null == sender)
			{
				throw new ArgumentNullException("sender");
			}

			_sender = sender;
		}

		/// <summary>
		/// Registers and event to be treated as a command.
		/// </summary>
		/// <typeparam name="Event">The event type.</typeparam>
		/// <returns>The <see cref="ISagaPublisher"/>.</returns>
		public ISagaPublisher Saga<Event>()
		{
			_events.Add(typeof(Event), null);
			return this;
		}

		/// <summary>
		/// Receives the published event.
		/// </summary>
		/// <param name="event">The published event.</param>
		public void Receive(object @event)
		{
			if (_events.ContainsKey(@event.GetType()))
			{
				Sender.Send(@event);
			}
		}
	}
}
