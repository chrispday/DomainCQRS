using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Provides a proxy for Message types.  The proxy is used to cache delegate calls to methods.
	/// </summary>
	public class MessageProxy : IMessageProxy
	{
		public MessageProxy(Type type)
		{
			if (null == type)
			{
				throw new ArgumentNullException("type");
			}
			_type = type;
		}

		public delegate IEnumerable<Guid> GetAggregateRootIdsDelegate(object message);
		private readonly Dictionary<Type, GetAggregateRootIdsDelegate> _getAggregateRootIdMethods = new Dictionary<Type, GetAggregateRootIdsDelegate>();

		private readonly Type _type;
		/// <summary>
		/// The message type to proxy
		/// </summary>
		public Type Type
		{
			get { return _type; }
		}

		private readonly List<IAggregateRootProxy> _aggregateRootProxies = new List<IAggregateRootProxy>();
		/// <summary>
		/// Get the aggregate roots registered for this message type.
		/// </summary>
		public IEnumerable<IAggregateRootProxy> AggregateRootProxies
		{
			get { return _aggregateRootProxies; }
		}

		/// <summary>
		/// Retrieves the aggregate root Ids from the message for a particular aggregate root type.
		/// </summary>
		/// <param name="aggregateRootType">The aggregate root type to get Ids for.</param>
		/// <param name="message">The message to get Ids from.</param>
		/// <returns>The Ids.</returns>
		public IEnumerable<Guid> GetAggregateRootIds(Type aggregateRootType, object message)
		{
			GetAggregateRootIdsDelegate getAggregateRootIds;
			if (!_getAggregateRootIdMethods.TryGetValue(aggregateRootType, out getAggregateRootIds))
			{
				throw new KeyNotFoundException();
			}

			return getAggregateRootIds(message);
		}

		/// <summary>
		/// Registers the property to use to retrieve Ids for the given aggregate root type.
		/// </summary>
		/// <param name="aggregateRootProxy">The proxy to the aggregate root type.</param>
		/// <param name="aggregateRootIdsProperty">The name of the property to use to get Ids.</param>
		/// <returns>An <see cref="IMessageProxy"/>.</returns>
		public IMessageProxy Register(IAggregateRootProxy aggregateRootProxy, string aggregateRootIdsProperty)
		{
			if (string.IsNullOrEmpty(aggregateRootIdsProperty))
			{
				throw new ArgumentNullException("aggregateRootIdsProperty");
			}

			if (_getAggregateRootIdMethods.ContainsKey(aggregateRootProxy.Type))
			{
				throw new RegistrationException(string.Format("{0} already registered for {1}.", aggregateRootProxy.Type, Type));
			}

			_aggregateRootProxies.Add(aggregateRootProxy);
			_getAggregateRootIdMethods[aggregateRootProxy.Type] = CreateGetAggregateRootIds(aggregateRootIdsProperty);

			return this;
		}

		private GetAggregateRootIdsDelegate CreateGetAggregateRootIds(string aggregateRootIdsProperty)
		{
			var aggregateRootIds = Type.GetProperty(aggregateRootIdsProperty);
			if (null == aggregateRootIds)
			{
				throw new RegistrationException(string.Format("Property {0}.{1} to get AggregateRootId(s) does not exist.", Type, aggregateRootIdsProperty));
			}

			if (typeof(Guid).IsAssignableFrom(aggregateRootIds.PropertyType))
			{
				return ILHelper.CreateGetAggregateRootIdDelegate(Type, aggregateRootIds);
			}
			else if (typeof(IEnumerable<Guid>).IsAssignableFrom(aggregateRootIds.PropertyType))
			{
				return ILHelper.CreateGetAggregateRootIdsDelegate(Type, aggregateRootIds);
			}

			throw new RegistrationException(string.Format("{0}.{1} does not return a Guid or IEnumerable<Guid>.", Type, aggregateRootIdsProperty));
		}
	}

	public static class MessageProxyExtensions
	{
		/// <summary>
		/// Creates a message proxy for a type.
		/// </summary>
		/// <param name="messageType">The message type.</param>
		/// <returns>An <see cref="IMessageProxy"/>.</returns>
		public static IMessageProxy CreateMessageProxy(this Type messageType)
		{
			return new MessageProxy(messageType);
		}
	}
}
