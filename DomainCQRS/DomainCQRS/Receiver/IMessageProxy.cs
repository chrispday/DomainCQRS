using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DomainCQRS
{
	/// <summary>
	/// Provides a proxy for Message types.  The proxy is used to cache delegate calls to methods.
	/// </summary>
	public interface IMessageProxy
	{
		/// <summary>
		/// The message type to proxy
		/// </summary>
		Type Type { get; }
		/// <summary>
		/// Retrieves the aggregate root Ids from the message for a particular aggregate root type.
		/// </summary>
		/// <param name="aggregateRootType">The aggregate root type to get Ids for.</param>
		/// <param name="message">The message to get Ids from.</param>
		/// <returns>The Ids.</returns>
		IEnumerable<Guid> GetAggregateRootIds(Type aggregateRootType, object message);
		
		/// <summary>
		/// Registers the property to use to retrieve Ids for the given aggregate root type.
		/// </summary>
		/// <param name="aggregateRootProxy">The proxy to the aggregate root type.</param>
		/// <param name="aggregateRootIdsProperty">The name of the property to use to get Ids.</param>
		/// <returns>An <see cref="IMessageProxy"/>.</returns>
		IMessageProxy Register(IAggregateRootProxy aggregateRootProxy, string aggregateRootIdsProperty);
		/// <summary>
		/// Get the aggregate roots registered for this message type.
		/// </summary>
		IEnumerable<IAggregateRootProxy> AggregateRootProxies { get; }
	}
}
