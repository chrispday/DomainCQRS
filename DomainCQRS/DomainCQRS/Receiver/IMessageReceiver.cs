using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Receives messages and applies them to an aggreagte root.
	/// </summary>
	public interface IMessageReceiver
	{
		ILogger Logger { get; }
		IEventStore EventStore { get; }
		IAggregateRootCache AggregateRootCache { get; }
		/// <summary>
		/// The default name of the property to use to get aggregate root Ids from messages.
		/// </summary>
		string DefaultAggregateRootIdProperty { get; }
		/// <summary>
		/// The default name of the method to use to apply messages to aggregate roots.
		/// </summary>
		string DefaultAggregateRootApplyMethod { get; }

		/// <summary>
		/// Receive a message.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <returns>An <see cref="IMessageReceiver"/>.</returns>
		IMessageReceiver Receive(object message);
		/// <summary>
		/// Register an aggregate root type to receive a message type.
		/// </summary>
		/// <typeparam name="Message">The message type.</typeparam>
		/// <typeparam name="AggregateRoot">The aggregate root type that will receive the message.</typeparam>
		/// <returns>An <see cref="IMessageReceiver"/>.</returns>
		IMessageReceiver Register<Message, AggregateRoot>();
		/// <summary>
		/// Register an aggregate root type to receive a message type.
		/// </summary>
		/// <typeparam name="Message">The message type.</typeparam>
		/// <typeparam name="AggregateRoot">The aggregate root type that will receive the message.</typeparam>
		/// <param name="aggregateRootIdsProperty">The name of the property to use to get aggregate root Ids from messages.</param>
		/// <param name="aggregateRootApplyMethod">The name of the method to use to apply messages to aggregate roots.</param>
		/// <returns>An <see cref="IMessageReceiver"/>.</returns>
		IMessageReceiver Register<Message, AggregateRoot>(string aggregateRootIdsProperty, string aggregateRootApplyMethod);
		/// <summary>
		/// If a message type has been registered with the <see cref="IMessageReceiver"/>.
		/// </summary>
		/// <param name="messageType">The message type to check.</param>
		/// <returns>If the message type is registered.</returns>
		bool IsRegistered(Type messageType);
	}
}
