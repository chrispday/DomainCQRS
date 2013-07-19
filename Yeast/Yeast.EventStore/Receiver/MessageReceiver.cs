using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public class MessageReceiver : IMessageReceiver
	{
		public IEventStore EventStore { get; set; }
		public string DefaultAggregateRootIdProperty { get; set; }
		public string DefaultAggregateRootApplyCommandMethod { get; set; }
		public int AggregateRootCacheSize { get; set; }
		public class AggregateRootAndVersion
		{
			public object AggregateRoot;
			public int Version;
		}
		private LRUDictionary<Guid, AggregateRootAndVersion> AggregateRootCache;

		public MessageReceiver()
		{
			DefaultAggregateRootIdProperty = "AggregateRootId";
			DefaultAggregateRootApplyCommandMethod = "Apply";
			AggregateRootCacheSize = 1000;
		}

		public IMessageReceiver Receive(object message)
		{
			var messageType = message.GetType();

			Dictionary<Type, List<PropertyAndMethod>> aggregateRootTypes;
			if (!_messages.TryGetValue(messageType, out aggregateRootTypes))
			{
				throw new RegistrationException(string.Format("{0} is not registered.", messageType.Name)) { MessageType = messageType };
			}

			if (null == AggregateRootCache)
			{
				AggregateRootCache = new LRUDictionary<Guid, AggregateRootAndVersion>(AggregateRootCacheSize);
			}

			foreach (var aggregateRootType in aggregateRootTypes)
			{
				foreach (var propertyAndMethod in aggregateRootType.Value)
				{
					foreach (var aggregateRootId in ExtractAggregateRootIdsFromMessage(messageType, propertyAndMethod.Property, message))
					{
						AggregateRootAndVersion aggregateRootAndVersion;
						if (!AggregateRootCache.TryGetValue(aggregateRootId, out aggregateRootAndVersion))
						{
							var aggregateRoot = CreateAggregateRoot(aggregateRootType.Key);
							var version = LoadAggreateRoot(aggregateRootType.Key, aggregateRoot, aggregateRootId);
							aggregateRootAndVersion = new AggregateRootAndVersion() { AggregateRoot = aggregateRoot, Version = version };
						}
						var eventsToStore = ApplyCommandToAggregate(messageType, aggregateRootType.Key, propertyAndMethod.Method, message, aggregateRootAndVersion.AggregateRoot);
						foreach (var @event in eventsToStore)
						{
							EventStore.Save(aggregateRootId, ++aggregateRootAndVersion.Version, @event);
						}
						AggregateRootCache[aggregateRootId] = aggregateRootAndVersion;
					}
				}
			}

			return this;
		}

		private delegate IEnumerable<Guid> GetAggregateRootIds(object message);
		private Dictionary<Type, Dictionary<PropertyInfo, GetAggregateRootIds>> _perMessageTypePerPropertyGetAggregateRootIds = new Dictionary<Type, Dictionary<PropertyInfo, GetAggregateRootIds>>();
		private IEnumerable<Guid> ExtractAggregateRootIdsFromMessage(Type messageType, PropertyInfo aggregateRootIdsProperty, object message)
		{
			Dictionary<PropertyInfo, GetAggregateRootIds> perPropertyGetAggregateRootIds;
			if (!_perMessageTypePerPropertyGetAggregateRootIds.TryGetValue(messageType, out perPropertyGetAggregateRootIds))
			{
				_perMessageTypePerPropertyGetAggregateRootIds.Add(messageType, perPropertyGetAggregateRootIds = new Dictionary<PropertyInfo, GetAggregateRootIds>());
			}

			GetAggregateRootIds getAggregateRootIds;
			if (!perPropertyGetAggregateRootIds.TryGetValue(aggregateRootIdsProperty, out getAggregateRootIds))
			{
				perPropertyGetAggregateRootIds.Add(aggregateRootIdsProperty, getAggregateRootIds = GetGetAggregateRootIdsDelegate(messageType, aggregateRootIdsProperty));
			}

			return getAggregateRootIds(message);
		}

		private GetAggregateRootIds GetGetAggregateRootIdsDelegate(Type messageType, PropertyInfo property)
		{
			return (typeof(Guid) == property.PropertyType)
				? CreateGetAggregateRootIdDelegate(messageType, property)
				: CreateGetAggregateRootIdsDelegate(messageType, property);
		}

		private delegate object CreateAggreateRoot();
		private Dictionary<Type, CreateAggreateRoot> _createAggregateRoots = new Dictionary<Type, CreateAggreateRoot>();
		private object CreateAggregateRoot(Type aggregateRootType)
		{
			CreateAggreateRoot createAggregateRoot;
			if (!_createAggregateRoots.TryGetValue(aggregateRootType, out createAggregateRoot))
			{
				var dynamicMethod = new DynamicMethod(aggregateRootType.Name + "_Create", typeof(object), null);
				var ilGenerator = dynamicMethod.GetILGenerator();
				ilGenerator.Emit(OpCodes.Newobj, aggregateRootType.GetConstructor(Type.EmptyTypes));
				ilGenerator.Emit(OpCodes.Ret);
				_createAggregateRoots.Add(aggregateRootType, createAggregateRoot = (CreateAggreateRoot)dynamicMethod.CreateDelegate(typeof(CreateAggreateRoot)));
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

		private delegate void ApplyEvent(object aggregateRoot, object @event);
		private Dictionary<Type, Dictionary<Type, ApplyEvent>> _perMessagePerAggregateRootApplyEvents = new Dictionary<Type, Dictionary<Type, ApplyEvent>>();
		private void ApplyEventToAggregate(Type eventType, Type aggregateRootType, object @event, object aggregateRoot)
		{
			Dictionary<Type, ApplyEvent> perAggregateApplyEvents;
			if (!_perMessagePerAggregateRootApplyEvents.TryGetValue(eventType, out perAggregateApplyEvents))
			{
				_perMessagePerAggregateRootApplyEvents.Add(eventType, perAggregateApplyEvents = new Dictionary<Type, ApplyEvent>());
			}

			ApplyEvent applyEvent;
			if (!perAggregateApplyEvents.TryGetValue(aggregateRootType, out applyEvent))
			{
				MethodInfo applyMethod = null;
				foreach (var method in aggregateRootType.GetMethods())
				{
					if (null == method.ReturnType)
					{
						continue;
					}

					var parameters = method.GetParameters();
					if (null == parameters)
					{
						continue;
					}

					if (1 != parameters.Length)
					{
						continue;
					}

					if (eventType != parameters[0].ParameterType)
					{
						continue;
					}

					applyMethod = method;
					break;
				}
				if (null == applyMethod)
				{
					throw new RegistrationException(string.Format("{0} does not contain a method to apply {1}.", aggregateRootType.Name, eventType.Name));
				}

				perAggregateApplyEvents.Add(aggregateRootType, applyEvent = CreateApplyEvent(eventType, aggregateRootType, applyMethod));
			}

			applyEvent(aggregateRoot, @event);
		}

		private ApplyEvent CreateApplyEvent(Type eventType, Type aggregateRootType, MethodInfo applyMethod)
		{
			var dynamicMethod = new DynamicMethod(string.Format("ApplyEvent_{0}_{1}", aggregateRootType.Name, eventType.Name), null, new Type[] { typeof(object), typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Nop);
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, aggregateRootType);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			ilGenerator.Emit(OpCodes.Castclass, eventType);
			ilGenerator.EmitCall(OpCodes.Callvirt, applyMethod, null);
			ilGenerator.Emit(OpCodes.Nop);
			ilGenerator.Emit(OpCodes.Ret);

			return (ApplyEvent)dynamicMethod.CreateDelegate(typeof(ApplyEvent));
		}

		private delegate IEnumerable ApplyCommand(object aggregateRoot, object command);
		private Dictionary<Type, Dictionary<Type, ApplyCommand>> _perMessagePerAggregateRootApplyCommands = new Dictionary<Type, Dictionary<Type, ApplyCommand>>();
		private IEnumerable ApplyCommandToAggregate(Type messageType, Type aggregateRootType, MethodInfo applyMethod, object command, object aggregateRoot)
		{
			Dictionary<Type, ApplyCommand> perAggregateApplyCommands;
			if (!_perMessagePerAggregateRootApplyCommands.TryGetValue(messageType, out perAggregateApplyCommands))
			{
				_perMessagePerAggregateRootApplyCommands.Add(messageType, perAggregateApplyCommands = new Dictionary<Type, ApplyCommand>());
			}

			ApplyCommand applyCommand;
			if (!perAggregateApplyCommands.TryGetValue(aggregateRootType, out applyCommand))
			{
				perAggregateApplyCommands.Add(aggregateRootType, applyCommand = CreateApplyCommand(messageType, aggregateRootType, applyMethod));
			}

			return applyCommand(aggregateRoot, command);
		}

		private ApplyCommand CreateApplyCommand(Type commandType, Type aggregateRootType, MethodInfo applyMethod)
		{
			var dynamicMethod = new DynamicMethod(string.Format("ApplyCommand_{0}_{1}", aggregateRootType.Name, commandType.Name), typeof(IEnumerable), new Type[] { typeof(object), typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Nop);
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, aggregateRootType);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			ilGenerator.Emit(OpCodes.Castclass, commandType);
			ilGenerator.EmitCall(OpCodes.Callvirt, applyMethod, null);
			ilGenerator.Emit(OpCodes.Nop);
			ilGenerator.Emit(OpCodes.Ret);

			return (ApplyCommand)dynamicMethod.CreateDelegate(typeof(ApplyCommand));
		}

		public IMessageReceiver Register<Message, AggregateRoot>()
		{
			return Register<Message, AggregateRoot>(DefaultAggregateRootIdProperty, DefaultAggregateRootApplyCommandMethod);
		}

		private class PropertyAndMethod
		{
			public PropertyInfo Property { get; set; }
			public MethodInfo Method { get; set; }
		}
		private Dictionary<Type, Dictionary<Type, List<PropertyAndMethod>>> _messages = new Dictionary<Type, Dictionary<Type, List<PropertyAndMethod>>>();
		public IMessageReceiver Register<Message, AggregateRoot>(string aggregateRootIdsProperty, string aggregateRootApplyCommandMethod)
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

			var applyMethod = aggregateRootType.GetMethod(aggregateRootApplyCommandMethod, new Type[] { messageType });
			if (null == applyMethod)
			{
				throw new RegistrationException();
			}
			if (typeof(object) == applyMethod.GetParameters()[0].ParameterType)
			{
				throw new RegistrationException();
			}
			if (!typeof(IEnumerable).IsAssignableFrom(applyMethod.ReturnType))
			{
				throw new RegistrationException();
			}

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

			return this;
		}

		#region Automagic Handling

		private GetAggregateRootIds CreateGetAggregateRootIdDelegate(Type messageType, PropertyInfo property)
		{
			var dynamicMethod = new DynamicMethod(messageType.Name + "_" + property.Name, typeof(IEnumerable<Guid>), new Type[] { typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			var guidArray = ilGenerator.DeclareLocal(typeof(Guid[]));
			var guidEnum = ilGenerator.DeclareLocal(typeof(IEnumerable<Guid>));
			ilGenerator.Emit(OpCodes.Ldc_I4_1);
			ilGenerator.Emit(OpCodes.Newarr, typeof(Guid));
			ilGenerator.Emit(OpCodes.Stloc_0);
			ilGenerator.Emit(OpCodes.Ldloc_0);
			ilGenerator.Emit(OpCodes.Ldc_I4_0);
			ilGenerator.Emit(OpCodes.Ldelema, typeof(Guid));
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, messageType);
			ilGenerator.EmitCall(OpCodes.Callvirt, property.GetGetMethod(), null);
			ilGenerator.Emit(OpCodes.Stobj, typeof(Guid));
			ilGenerator.Emit(OpCodes.Ldloc_0);
			ilGenerator.Emit(OpCodes.Stloc_1);
			ilGenerator.Emit(OpCodes.Ldloc_1);
			ilGenerator.Emit(OpCodes.Ret);

			return (GetAggregateRootIds)dynamicMethod.CreateDelegate(typeof(GetAggregateRootIds));
		}

		private GetAggregateRootIds CreateGetAggregateRootIdsDelegate(Type messageType, PropertyInfo property)
		{
			var dynamicMethod = new DynamicMethod("GetAggregateRootIds_" + messageType.Name, typeof(IEnumerable<Guid>), new Type[] { typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, messageType);
			ilGenerator.EmitCall(OpCodes.Callvirt, property.GetGetMethod(), null);
			ilGenerator.Emit(OpCodes.Castclass, typeof(IEnumerable<Guid>));
			ilGenerator.Emit(OpCodes.Ret);

			return (GetAggregateRootIds)dynamicMethod.CreateDelegate(typeof(GetAggregateRootIds));
		}

		#endregion
	
}
}
