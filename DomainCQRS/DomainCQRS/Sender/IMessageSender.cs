using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Sends messages.
	/// </summary>
	public interface IMessageSender
	{
		ILogger Logger { get; }

		/// <summary>
		/// Sends the message to be received by the <see cref="IMessageReceiver"/>
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <returns>The <see cref="IMessageSender"/></returns>
		IMessageSender Send(object message);
	}
}
