using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Publishes events to the message receiver to be consumed by Aggregate Roots as commands.
	/// </summary>
	public interface ISagaPublisher : IEventProjector<object>
	{
		/// <summary>
		/// The <see cref="IMessageSender"/> to use to send the events to the <see cref="IMessageReceiver"/>.
		/// </summary>
		IMessageSender Sender { get; }
		/// <summary>
		/// Registers an event to be treated as a command.
		/// </summary>
		/// <typeparam name="Event">The event type.</typeparam>
		/// <returns>The <see cref="ISagaPublisher"/>.</returns>
		ISagaPublisher Saga<Event>();
	}
}
