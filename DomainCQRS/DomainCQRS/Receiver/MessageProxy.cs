using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
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
		public Type Type
		{
			get { return _type; }
		}

		private readonly List<IAggregateRootProxy> _aggregateRootProxies = new List<IAggregateRootProxy>();
		public IEnumerable<IAggregateRootProxy> AggregateRootProxies
		{
			get { return _aggregateRootProxies; }
		}

		public IEnumerable<Guid> GetAggregateRootIds(Type aggregateRootType, object message)
		{
			GetAggregateRootIdsDelegate getAggregateRootIds;
			if (!_getAggregateRootIdMethods.TryGetValue(aggregateRootType, out getAggregateRootIds))
			{
				throw new KeyNotFoundException();
			}

			return getAggregateRootIds(message);
		}

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
		public static IMessageProxy CreateMessageProxy(this Type messageType)
		{
			return new MessageProxy(messageType);
		}
	}
}
