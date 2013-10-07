using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class MessageReceiverConfigure
	{
		public static readonly string DefaultAggregateRootIdProperty = "AggregateRootId";
		public static readonly string DefaultAggregateRootApplyMethod = "Apply";
		public static IConfigure MessageReceiver(this IConfigure configure) { return MessageReceiver(configure, DefaultAggregateRootIdProperty, DefaultAggregateRootApplyMethod); }
		public static IConfigure MessageReceiver(this IConfigure configure, string defaultAggregateRootIdProperty) { return MessageReceiver(configure, defaultAggregateRootIdProperty, DefaultAggregateRootApplyMethod); }
		public static IConfigure MessageReceiver(this IConfigure configure, string defaultAggregateRootIdProperty, string defaultAggregateRootApplyMethod)
		{
			configure.Registry
				.BuildInstancesOf<IMessageReceiver>()
				.TheDefaultIs(Registry.Instance<IMessageReceiver>()
					.UsingConcreteType<MessageReceiver>()
					.WithProperty("defaultAggregateRootIdProperty").EqualTo(defaultAggregateRootIdProperty)
					.WithProperty("defaultAggregateRootApplyMethod").EqualTo(defaultAggregateRootApplyMethod))
				.AsSingletons();
			return configure;
		}

		public static IBuiltConfigure Register<Message, AggregateRoot>(this IBuiltConfigure configure) { return Register<Message, AggregateRoot>(configure, null, null); }
		public static IBuiltConfigure Register<Message, AggregateRoot>(this IBuiltConfigure configure, string aggregateRootIdProperty) { return Register<Message, AggregateRoot>(configure, aggregateRootIdProperty, null); }
		public static IBuiltConfigure Register<Message, AggregateRoot>(this IBuiltConfigure configure, string aggregateRootIdsProperty, string aggregateRootApplyCommandMethod)
		{
			configure.MessageReceiver
				.Register<Message, AggregateRoot>(
					aggregateRootIdsProperty ?? configure.MessageReceiver.DefaultAggregateRootIdProperty,
					aggregateRootApplyCommandMethod ?? configure.MessageReceiver.DefaultAggregateRootApplyMethod);
			return configure;
		}
	}

	public class MessageReceiver : IMessageReceiver
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly IEventStore _eventStore;
		public IEventStore EventStore { get { return _eventStore; } }
		private readonly IAggregateRootCache _aggregateRootCache;
		public IAggregateRootCache AggregateRootCache { get { return _aggregateRootCache; } }
		private readonly string _defaultAggregateRootIdProperty;
		public string DefaultAggregateRootIdProperty { get { return _defaultAggregateRootIdProperty; } }
		private readonly string _defaultAggregateRootApplyMethod;
		public string DefaultAggregateRootApplyMethod { get { return _defaultAggregateRootApplyMethod; } }

		public MessageReceiver(ILogger logger, IEventStore eventStore, IAggregateRootCache aggregateRootCache, string defaultAggregateRootIdProperty, string defaultAggregateRootApplyMethod)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == eventStore)
			{
				throw new ArgumentNullException("eventStore");
			}
			if (null == aggregateRootCache)
			{
				throw new ArgumentNullException("aggregateRootCache");
			}
			if (null == defaultAggregateRootIdProperty)
			{
				throw new ArgumentNullException("defaultAggregateRootIdProperty");
			}
			if (null == defaultAggregateRootApplyMethod)
			{
				throw new ArgumentNullException("defaultAggregateRootApplyMethod");
			}

			_logger = logger;
			_eventStore = eventStore;
			_aggregateRootCache = aggregateRootCache;
			_defaultAggregateRootIdProperty = defaultAggregateRootIdProperty;
			_defaultAggregateRootApplyMethod = defaultAggregateRootApplyMethod;
		}

		public IMessageReceiver Receive(object message)
		{
			Logger.Verbose("Received message {0}", message ?? "<NULL>");

			var messageType = message.GetType();

			IMessageProxy messageProxy;
			if (!_messageProxies.TryGetValue(messageType, out messageProxy))
			{
				throw new RegistrationException(string.Format("{0} is not registered.", messageType));
			}

			foreach (var aggregateRootProxy in messageProxy.AggregateRootProxies)
			{
				foreach (var aggregateRootId in messageProxy.GetAggregateRootIds(aggregateRootProxy.Type, message))
				{
					AggregateRootAndVersion aggregateRootAndVersion = GetAggregateRootAndVersion(aggregateRootProxy, aggregateRootId);
					var eventsToStore = aggregateRootProxy.ApplyCommand(aggregateRootAndVersion.AggregateRoot, message);
					foreach (var @event in eventsToStore)
					{
						EventStore.Save(aggregateRootId, ++aggregateRootAndVersion.LatestVersion, aggregateRootProxy.Type, @event);
					}
				}
			}

			return this;
		}

		private AggregateRootAndVersion GetAggregateRootAndVersion(IAggregateRootProxy aggregateRootProxy, Guid aggregateRootId)
		{
			AggregateRootAndVersion aggregateRootAndVersion;
			if (!AggregateRootCache.TryGetValue(aggregateRootId, out aggregateRootAndVersion))
			{
				var aggregateRoot = aggregateRootProxy.Create();
				var version = LoadAggreateRoot(aggregateRootProxy, aggregateRoot, aggregateRootId);
				AggregateRootCache[aggregateRootId] = aggregateRootAndVersion = new AggregateRootAndVersion() { AggregateRoot = aggregateRoot, LatestVersion = version };
			}
			return aggregateRootAndVersion;
		}

		private int LoadAggreateRoot(IAggregateRootProxy aggregateRootProxy, object aggregateRoot, Guid aggregateRootId)
		{
			var events = EventStore.Load(aggregateRootId, null, null, null, null);

			int version = 0;
			foreach (var @event in events)
			{
				if (version < @event.Version)
				{
					version = @event.Version;
				}

				aggregateRootProxy.ApplyEvent(aggregateRoot, @event.Event);
			}

			return version;
		}

		public bool IsRegistered(Type messageType)
		{
			return _messageProxies.ContainsKey(messageType);
		}

		public IMessageReceiver Register<Message, AggregateRoot>()
		{
			return Register<Message, AggregateRoot>(DefaultAggregateRootIdProperty, DefaultAggregateRootApplyMethod);
		}
		
		private Dictionary<Type, IMessageProxy> _messageProxies = new Dictionary<Type, IMessageProxy>();
		private Dictionary<Type, IAggregateRootProxy> _aggregateRootProxies = new Dictionary<Type, IAggregateRootProxy>();
		public IMessageReceiver Register<Message, AggregateRoot>(string aggregateRootIdsProperty, string aggregateRootApplyMethod)
		{
			var messageType = typeof(Message);
			var aggregateRootType = typeof(AggregateRoot);

			IMessageProxy messageProxy;
			if (!_messageProxies.TryGetValue(messageType, out messageProxy))
			{
				_messageProxies[messageType] = messageProxy = messageType.CreateMessageProxy();
			}

			IAggregateRootProxy aggregateRootProxy;
			if (!_aggregateRootProxies.TryGetValue(aggregateRootType, out aggregateRootProxy))
			{
				_aggregateRootProxies[aggregateRootType] = aggregateRootProxy = aggregateRootType.CreateAggregateRootProxy();
			}

			messageProxy.Register(aggregateRootProxy, aggregateRootIdsProperty);
			aggregateRootProxy.Register(messageProxy, aggregateRootApplyMethod);

			return this;
		}
	}
}
