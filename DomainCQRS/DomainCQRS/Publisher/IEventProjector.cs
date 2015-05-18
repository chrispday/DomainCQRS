using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Subscribes to events and projects <typeparamref name="Event"/>
	/// </summary>
	/// <typeparam name="Event">The event types this projector handles.</typeparam>
	public interface IEventProjector<Event>
	{
		/// <summary>
		/// The subscripion id to use when registering with the publisher.
		/// Should be implemented as a static readonly Guid so the same id is used every time.
		/// </summary>
		Guid SubscriptionId { get; }
		/// <summary>
		/// Receives the published event.
		/// </summary>
		/// <param name="event">The published event.</param>
		void Receive(Event @event);
	}
}
