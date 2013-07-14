using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Yeast.EventStore
{
	public class MessageReceiver : IMessageReceiver
	{
		public IEventStore EventStore { get; set; }
		public string DefaultAggregateRootIdProperty { get; set; }
		public string DefaultAggregateRootApplyCommandMethod { get; set; }

		public MessageReceiver()
		{
			DefaultAggregateRootIdProperty = "AggregateRootId";
			DefaultAggregateRootApplyCommandMethod = "Apply";
		}

		public void Receive(object message)
		{
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
						var aggregateRoot = CreateAggregateRoot(aggregateRootType.Key);
						var messages = EventStore.Load(aggregateRootId, null, null, null, null);
						var version = LoadAggreateRoot(aggregateRootType.Key, propertyAndMethod.Method, aggregateRoot, messages);
						var events = ApplyMessageToAggregate(messageType, aggregateRootType.Key, propertyAndMethod.Method, message, aggregateRoot);
						foreach (var @event in events)
						{
							EventStore.Save(aggregateRootId, ++version, @event);
						}
					}
				}
			}
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

		private int LoadAggreateRoot(Type aggregateRootType, MethodInfo applyMethod, object aggregateRoot, IEnumerable<StoredEvent> messages)
		{
			int version = -1;
			foreach (var message in messages)
			{
				if (version < message.Version)
				{
					version = message.Version;
				}

				ApplyMessageToAggregate(message.Event.GetType(), aggregateRootType, applyMethod, message.Event, aggregateRoot);
			}

			return version;
		}

		private delegate IEnumerable ApplyMessage(object aggregateRoot, object message);
		private Dictionary<Type, Dictionary<Type, ApplyMessage>> _perMessagePerAggregateRootApplyMessages = new Dictionary<Type, Dictionary<Type, ApplyMessage>>();
		private IEnumerable ApplyMessageToAggregate(Type messageType, Type aggregateRootType, MethodInfo applyMethod, object message, object aggregateRoot)
		{
			Dictionary<Type, ApplyMessage> perAggregateApplyMessages;
			if (!_perMessagePerAggregateRootApplyMessages.TryGetValue(messageType, out perAggregateApplyMessages))
			{
				_perMessagePerAggregateRootApplyMessages.Add(messageType, perAggregateApplyMessages = new Dictionary<Type, ApplyMessage>());
			}

			ApplyMessage applyMessage;
			if (!perAggregateApplyMessages.TryGetValue(aggregateRootType, out applyMessage))
			{
				perAggregateApplyMessages.Add(aggregateRootType, applyMessage = CreateApplyMessage(messageType, aggregateRootType, applyMethod));
			}

			return applyMessage(aggregateRoot, message);
		}

		private ApplyMessage CreateApplyMessage(Type messageType, Type aggregateRootType, MethodInfo applyMethod)
		{
			var dynamicMethod = new DynamicMethod(string.Format("Apply_{0}_{1}", aggregateRootType.Name, messageType.Name), typeof(IEnumerable), new Type[] { typeof(object), typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Nop);
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, aggregateRootType);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			ilGenerator.Emit(OpCodes.Castclass, messageType);
			ilGenerator.EmitCall(OpCodes.Callvirt, applyMethod, null);
			ilGenerator.Emit(OpCodes.Nop);
			ilGenerator.Emit(OpCodes.Ret);

			return (ApplyMessage)dynamicMethod.CreateDelegate(typeof(ApplyMessage));
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
