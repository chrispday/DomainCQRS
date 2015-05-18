using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Provides a proxy for Aggregate Root types.  The proxy is used to cache delegate calls to methods.
	/// </summary>
	public interface IAggregateRootProxy
	{
		/// <summary>
		/// The aggregate root type to proxy
		/// </summary>
		Type Type { get; }

		/// <summary>
		/// Create an object using the empty constructor.
		/// </summary>
		/// <returns></returns>
		object Create();
		/// <summary>
		/// Applies a command to the aggregate root.
		/// </summary>
		/// <param name="aggregateRoot">The aggregate root to apply the command to.</param>
		/// <param name="command">The command to apply.</param>
		/// <returns>The events generated from the aggregate root applying the command.</returns>
		IEnumerable ApplyCommand(object aggregateRoot, object command);
		/// <summary>
		/// Applies a historical event to the aggregate root.
		/// </summary>
		/// <param name="aggregateRoot">The aggregate root.</param>
		/// <param name="event">The event to apply.</param>
		void ApplyEvent(object aggregateRoot, object @event);

		/// <summary>
		/// Register a command (message) that can be applied to the aggregate root type.
		/// </summary>
		/// <param name="messageProxy">The <see cref="IMessageProxy"/> for the command type.</param>
		/// <param name="aggregateRootApplyMethod">The name of the method that will apply the message type.</param>
		/// <returns>The <see cref="IAggregateRootProxy"/>.</returns>
		IAggregateRootProxy Register(IMessageProxy messageProxy, string aggregateRootApplyMethod);
	}
}
