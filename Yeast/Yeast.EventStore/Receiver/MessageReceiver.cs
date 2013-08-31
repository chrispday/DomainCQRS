using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public static class MessageReceiverConfigure
	{
		public static readonly string DefaultAggregateRootIdProperty = "AggregateRootId";
		public static readonly string DefaultAggregateRootApplyMethod = "Apply";
		public static IConfigure MessageReceiver(this IConfigure configure) { return MessageReceiver(configure, DefaultAggregateRootIdProperty, DefaultAggregateRootApplyMethod); }
		public static IConfigure MessageReceiver(this IConfigure configure, string defaultAggregateRootIdProperty, string defaultAggregateRootApplyMethod)
		{
			var c = configure as Configure;
			c.MessageReceiver = new MessageReceiver()
			{
				Logger = c.Logger, 
				EventStore = c.EventStore, 
				AggregateRootCache = c.AggregateRootCache, 
				DefaultAggregateRootIdProperty = defaultAggregateRootIdProperty,
				DefaultAggregateRootApplyMethod = defaultAggregateRootApplyMethod,
				EventPublisher = c.EventPublisher
			};
			return configure;
		}

		public static IConfigure Register<Message, AggregateRoot>(this IConfigure configure) { return Register<Message, AggregateRoot>(configure, DefaultAggregateRootIdProperty, DefaultAggregateRootApplyMethod); }
		public static IConfigure Register<Message, AggregateRoot>(this IConfigure configure, string aggregateRootIdProperty) { return Register<Message, AggregateRoot>(configure, aggregateRootIdProperty, DefaultAggregateRootApplyMethod); }
		public static IConfigure Register<Message, AggregateRoot>(this IConfigure configure, string aggregateRootIdsProperty, string aggregateRootApplyCommandMethod)
		{
			var c = configure as Configure;
			c.MessageReceiver.Register<Message, AggregateRoot>(aggregateRootIdsProperty, aggregateRootApplyCommandMethod);
			return configure;
		}
	}

	public delegate object CreateAggreateRoot();
	public delegate void ApplyEvent(object aggregateRoot, object @event);
	public delegate IEnumerable<Guid> GetAggregateRootIds(object message);
	public delegate IEnumerable ApplyEnumerableCommand(object aggregateRoot, object command);
	public delegate object ApplyObjectCommand(object aggregateRoot, object command);

	public class MessageReceiver : IMessageReceiver
	{
		public ILogger Logger { get; set; }
		public IEventStore EventStore { get; set; }
		public IAggregateRootCache AggregateRootCache { get; set; }
		public string DefaultAggregateRootIdProperty { get; set; }
		public string DefaultAggregateRootApplyMethod { get; set; }
		public bool Synchronous { get; set; }
		public IEventPublisher EventPublisher { get; set; }

		public MessageReceiver()
		{
			DefaultAggregateRootIdProperty = MessageReceiverConfigure.DefaultAggregateRootIdProperty;
			DefaultAggregateRootApplyMethod = MessageReceiverConfigure.DefaultAggregateRootApplyMethod;
		}

		public IMessageReceiver Receive(object message)
		{
			Logger.Verbose("Received message {0}", message ?? "<NULL>");

			var messageType = message.GetType();

			Dictionary<Type, List<PropertyAndMethod>> aggregateRootTypes;
			if (!_messages.TryGetValue(messageType, out aggregateRootTypes))
			{
				throw new RegistrationException(string.Format("{0} is not registered.", messageType.Name)) { MessageType = messageType };
			}

			foreach (var aggregateRootType in aggregateRootTypes)
			{
				foreach (var propertyAndMethod in aggregateRootType.Value)
				{
					foreach (var aggregateRootId in ExtractAggregateRootIdsFromMessage(messageType, propertyAndMethod.Property, message))
					{
						AggregateRootAndVersion aggregateRootAndVersion = GetAggregateRootAndVersion(aggregateRootType.Key, aggregateRootId);
						var eventsToStore = ApplyCommandToAggregate(messageType, aggregateRootType.Key, propertyAndMethod.Method, message, aggregateRootAndVersion.AggregateRoot);
						foreach (var @event in eventsToStore)
						{
							EventStore.Save(aggregateRootId, ++aggregateRootAndVersion.LatestVersion, @event);
							if (Synchronous)
							{
								EventPublisher.Publish(@event);
							}
						}
					}
				}
			}

			return this;
		}

		private AggregateRootAndVersion GetAggregateRootAndVersion(Type aggregateRootType, Guid aggregateRootId)
		{
			AggregateRootAndVersion aggregateRootAndVersion;
			if (!AggregateRootCache.TryGetValue(aggregateRootId, out aggregateRootAndVersion))
			{
				var aggregateRoot = CreateAggregateRoot(aggregateRootType);
				var version = LoadAggreateRoot(aggregateRootType, aggregateRoot, aggregateRootId);
				AggregateRootCache[aggregateRootId] = aggregateRootAndVersion = new AggregateRootAndVersion() { AggregateRoot = aggregateRoot, LatestVersion = version };
			}
			return aggregateRootAndVersion;
		}

		private Dictionary<Type, Dictionary<PropertyInfo, GetAggregateRootIds>> _perMessageTypePerPropertyGetAggregateRootIds = new Dictionary<Type, Dictionary<PropertyInfo, GetAggregateRootIds>>();
		private IEnumerable<Guid> ExtractAggregateRootIdsFromMessage(Type messageType, PropertyInfo aggregateRootIdsProperty, object message)
		{
			Dictionary<PropertyInfo, GetAggregateRootIds> perPropertyGetAggregateRootIds;
			if (!_perMessageTypePerPropertyGetAggregateRootIds.TryGetValue(messageType, out perPropertyGetAggregateRootIds))
			{
				lock (_perMessageTypePerPropertyGetAggregateRootIds)
				{
					if (!_perMessageTypePerPropertyGetAggregateRootIds.TryGetValue(messageType, out perPropertyGetAggregateRootIds))
					{
						_perMessageTypePerPropertyGetAggregateRootIds.Add(messageType, perPropertyGetAggregateRootIds = new Dictionary<PropertyInfo, GetAggregateRootIds>());
					}
				}
			}

			GetAggregateRootIds getAggregateRootIds;
			if (!perPropertyGetAggregateRootIds.TryGetValue(aggregateRootIdsProperty, out getAggregateRootIds))
			{
				lock (perPropertyGetAggregateRootIds)
				{
					if (!perPropertyGetAggregateRootIds.TryGetValue(aggregateRootIdsProperty, out getAggregateRootIds))
					{
						perPropertyGetAggregateRootIds.Add(aggregateRootIdsProperty, getAggregateRootIds = GetGetAggregateRootIdsDelegate(messageType, aggregateRootIdsProperty));
					}
				}
			}

			return getAggregateRootIds(message);
		}

		private GetAggregateRootIds GetGetAggregateRootIdsDelegate(Type messageType, PropertyInfo property)
		{
			return (typeof(Guid) == property.PropertyType)
				? ILHelper.CreateGetAggregateRootIdDelegate(messageType, property)
				: ILHelper.CreateGetAggregateRootIdsDelegate(messageType, property);
		}

		private Dictionary<Type, CreateAggreateRoot> _createAggregateRoots = new Dictionary<Type, CreateAggreateRoot>();
		private object CreateAggregateRoot(Type aggregateRootType)
		{
			CreateAggreateRoot createAggregateRoot;
			if (!_createAggregateRoots.TryGetValue(aggregateRootType, out createAggregateRoot))
			{
				lock (_createAggregateRoots)
				{
					if (!_createAggregateRoots.TryGetValue(aggregateRootType, out createAggregateRoot))
					{
						_createAggregateRoots.Add(aggregateRootType, createAggregateRoot = ILHelper.CreateCreateAggreateRoot(aggregateRootType, createAggregateRoot));
					}
				}
			}

			return createAggregateRoot();
		}

		private int LoadAggreateRoot(Type aggregateRootType, object aggregateRoot, Guid aggregateRootId)
		{
			var events = EventStore.Load(aggregateRootId, null, null, null, null);

			int version = 0;
			foreach (var @event in events)
			{
				if (version < @event.Version)
				{
					version = @event.Version;
				}

				ApplyEventToAggregate(@event.Event.GetType(), aggregateRootType, @event.Event, aggregateRoot);
			}

			return version;
		}

		private Dictionary<Type, Dictionary<Type, ApplyEvent>> _perMessagePerAggregateRootApplyEvents = new Dictionary<Type, Dictionary<Type, ApplyEvent>>();
		private void ApplyEventToAggregate(Type eventType, Type aggregateRootType, object @event, object aggregateRoot)
		{
			Dictionary<Type, ApplyEvent> perAggregateApplyEvents;
			if (!_perMessagePerAggregateRootApplyEvents.TryGetValue(eventType, out perAggregateApplyEvents))
			{
				lock (_perMessagePerAggregateRootApplyEvents)
				{
					if (!_perMessagePerAggregateRootApplyEvents.TryGetValue(eventType, out perAggregateApplyEvents))
					{
						_perMessagePerAggregateRootApplyEvents.Add(eventType, perAggregateApplyEvents = new Dictionary<Type, ApplyEvent>());
					}
				}
			}

			ApplyEvent applyEvent;
			if (!perAggregateApplyEvents.TryGetValue(aggregateRootType, out applyEvent))
			{
				lock (perAggregateApplyEvents)
				{
					if (!perAggregateApplyEvents.TryGetValue(aggregateRootType, out applyEvent))
					{
						perAggregateApplyEvents.Add(aggregateRootType, applyEvent = ILHelper.CreateApplyEvent(eventType, aggregateRootType));
					}
				}
			}

			applyEvent(aggregateRoot, @event);
		}

		private Dictionary<Type, Dictionary<Type, Delegate>> _perMessagePerAggregateRootApplyCommands = new Dictionary<Type, Dictionary<Type, Delegate>>();
		private IEnumerable ApplyCommandToAggregate(Type messageType, Type aggregateRootType, MethodInfo applyMethod, object command, object aggregateRoot)
		{
			Dictionary<Type, Delegate> perAggregateApplyCommands;
			if (!_perMessagePerAggregateRootApplyCommands.TryGetValue(messageType, out perAggregateApplyCommands))
			{
				lock (_perMessagePerAggregateRootApplyCommands)
				{
					if (!_perMessagePerAggregateRootApplyCommands.TryGetValue(messageType, out perAggregateApplyCommands))
					{
						_perMessagePerAggregateRootApplyCommands.Add(messageType, perAggregateApplyCommands = new Dictionary<Type, Delegate>());
					}
				}
			}

			Delegate applyCommand;
			if (!perAggregateApplyCommands.TryGetValue(aggregateRootType, out applyCommand))
			{
				lock (perAggregateApplyCommands)
				{
					if (!perAggregateApplyCommands.TryGetValue(aggregateRootType, out applyCommand))
					{
						perAggregateApplyCommands.Add(aggregateRootType, applyCommand = ILHelper.CreateApplyCommand(messageType, aggregateRootType, applyMethod));
					}
				}
			}

			var enumerableApplyCommand = applyCommand as ApplyEnumerableCommand;
			if (null != enumerableApplyCommand)
			{
				return enumerableApplyCommand(aggregateRoot, command);
			}
			else
			{
				return new object[] { (applyCommand as ApplyObjectCommand)(aggregateRoot, command) };
			}
		}

		public bool IsRegistered(Type messageType)
		{
			return _messages.ContainsKey(messageType);
		}

		public IMessageReceiver Register<Message, AggregateRoot>()
		{
			return Register<Message, AggregateRoot>(DefaultAggregateRootIdProperty, DefaultAggregateRootApplyMethod);
		}

		private class PropertyAndMethod
		{
			public PropertyInfo Property { get; set; }
			public MethodInfo Method { get; set; }
		}
		private Dictionary<Type, Dictionary<Type, List<PropertyAndMethod>>> _messages = new Dictionary<Type, Dictionary<Type, List<PropertyAndMethod>>>();
		public IMessageReceiver Register<Message, AggregateRoot>(string aggregateRootIdsProperty, string aggregateRootApplyMethod)
		{
			if (string.IsNullOrEmpty(aggregateRootIdsProperty))
			{
				throw new ArgumentNullException("aggregateRootIdsProperty");
			}

			var messageType = typeof(Message);
			var aggregateRootType = typeof(AggregateRoot);

			var aggregateRootIds = messageType.GetProperty(aggregateRootIdsProperty);
			if (null == aggregateRootIds)
			{
				throw new RegistrationException(string.Format("Property {0}.{1} to get AggregateRootId(s) does not exist.", messageType.Name, aggregateRootIdsProperty));
			}
			if (!typeof(Guid).IsAssignableFrom(aggregateRootIds.PropertyType)
				&& !typeof(IEnumerable<Guid>).IsAssignableFrom(aggregateRootIds.PropertyType))
			{
				throw new RegistrationException(string.Format("{0}.{1} does not return a Guid or IEnumerable<Guid>.", messageType.Name, aggregateRootIdsProperty));
			}

			var constructor = aggregateRootType.GetConstructor(Type.EmptyTypes);
			if (null == constructor)
			{
				throw new RegistrationException(string.Format("{0} does not have an empty constructor.", aggregateRootType.Name));
			}

			var applyMethod = aggregateRootType.GetMethod(aggregateRootApplyMethod, new Type[] { messageType });
			if (null == applyMethod)
			{
				throw new RegistrationException();
			}
			if (typeof(object) == applyMethod.GetParameters()[0].ParameterType)
			{
				throw new RegistrationException();
			}

			lock (_messages)
			{
				Dictionary<Type, List<PropertyAndMethod>> aggregateRoots;
				if (!_messages.TryGetValue(messageType, out aggregateRoots))
				{
					_messages.Add(messageType, aggregateRoots = new Dictionary<Type, List<PropertyAndMethod>>());
				}

				List<PropertyAndMethod> propertyAndMethods;
				if (!aggregateRoots.TryGetValue(aggregateRootType, out propertyAndMethods))
				{
					aggregateRoots.Add(aggregateRootType, propertyAndMethods = new List<PropertyAndMethod>());
				}

				propertyAndMethods.Add(new PropertyAndMethod() { Property = aggregateRootIds, Method = applyMethod });
			}

			return this;
		}
	}
}
