using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DomainCQRS.Common
{
	/// <summary>
	/// Contains helper methods to create dynamic method delegates.
	/// </summary>
	public static class ILHelper
	{
		/// <summary>
		/// Create an event upgrader delegate method.
		/// </summary>
		/// <param name="eventType">The <see cref="Type"/> of the original event.</param>
		/// <param name="upgradedEventType">The <see cref="Type"/> of the event it will be upgraded to.</param>
		/// <returns>An <see cref="EventUpgrader"/> delegate.</returns>
		public static EventUpgrader CreateEventUpgrader(Type eventType, Type upgradedEventType)
		{
			var upgradedEventConstructor = upgradedEventType.GetConstructor(new Type[] { eventType });
			if (null == upgradedEventConstructor)
			{
				throw new ArgumentOutOfRangeException(string.Format("{0} does not have a constructor \"public {0}({1})\".", upgradedEventType.Name, eventType.Name));
			}

			var dynamicMethod = new DynamicMethod(upgradedEventType.Name + "_Upgrade", typeof(object), new Type[] { typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, eventType);
			ilGenerator.Emit(OpCodes.Newobj, upgradedEventConstructor);
			ilGenerator.Emit(OpCodes.Ret);
			return (EventUpgrader)dynamicMethod.CreateDelegate(typeof(EventUpgrader));
		}

		/// <summary>
		/// Create a message receiver delegate method.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber to an event.</typeparam>
		/// <typeparam name="Event">The event that is being subscribed to.</typeparam>
		/// <param name="subscriberReceiveMethodName">The name of the method on the subscriber that will recevie the event.</param>
		/// <returns>A <see cref="Receive"/> delegate.</returns>
		public static Receive CreateReceive<Subscriber, Event>(string subscriberReceiveMethodName)
		{
			var subscriberType = typeof(Subscriber);
			var eventType = typeof(Event);
			MethodInfo receiveMethod = subscriberType.GetMethod(subscriberReceiveMethodName, new Type[] { eventType }, true);
			if (null == receiveMethod
				|| typeof(void) != receiveMethod.ReturnType)
			{
				throw new RegistrationException(string.Format("{0} does not contain a method void {1}({2}).", subscriberType.Name, subscriberReceiveMethodName, eventType.Name));
			}

			var dynamicMethod = new DynamicMethod(string.Format("Receive_{0}_{1}", subscriberType.Name, eventType.Name), null, new Type[] { typeof(object), typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, subscriberType);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			ilGenerator.Emit(OpCodes.Castclass, eventType);
			ilGenerator.EmitCall(OpCodes.Callvirt, receiveMethod, null);
			ilGenerator.Emit(OpCodes.Ret);

			return (Receive)dynamicMethod.CreateDelegate(typeof(Receive));
		}

		/// <summary>
		/// Creates a delegate that constructs an Aggregate Root.
		/// </summary>
		/// <param name="aggregateRootType">The <see cref="Type"/> of the Aggregate Root.</param>
		/// <returns>An <see cref="AggregateRootProxy.CreateAggreateRootDelegate"/> delegate.</returns>
		public static AggregateRootProxy.CreateAggreateRootDelegate CreateCreateAggreateRoot(Type aggregateRootType)
		{
			var dynamicMethod = new DynamicMethod(aggregateRootType.Name + "_Create", typeof(object), null);
			var ilGenerator = dynamicMethod.GetILGenerator();
			ilGenerator.Emit(OpCodes.Newobj, aggregateRootType.GetConstructor(Type.EmptyTypes));
			ilGenerator.Emit(OpCodes.Ret);
			return (AggregateRootProxy.CreateAggreateRootDelegate)dynamicMethod.CreateDelegate(typeof(AggregateRootProxy.CreateAggreateRootDelegate));
		}

		/// <summary>
		/// Creates a delegate that calls the Aggregate Root method that applies an event.
		/// </summary>
		/// <param name="eventType">The <see cref="Type"/> of the event.</param>
		/// <param name="aggregateRootType">The <see cref="Type"/> of the Aggregate Root.</param>
		/// <returns><see cref="AggregateRootProxy.ApplyEventDelegate"/> delegate.</returns>
		public static AggregateRootProxy.ApplyEventDelegate CreateApplyEvent(Type eventType, Type aggregateRootType)
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

			return (AggregateRootProxy.ApplyEventDelegate)dynamicMethod.CreateDelegate(typeof(AggregateRootProxy.ApplyEventDelegate));
		}

		/// <summary>
		/// Creates a delegate to apply a command to an Aggregate Root
		/// </summary>
		/// <param name="commandType">The <see cref="Type"/> of the command.</param>
		/// <param name="aggregateRootType">The <see cref="Type"/> of the Aggregate Root.</param>
		/// <param name="applyMethod">The name of the Aggregate Root's method that will appy the command.</param>
		/// <returns>A <see cref="Delegate"/></returns>
		public static Delegate CreateApplyCommand(Type commandType, Type aggregateRootType, MethodInfo applyMethod)
		{
			var enumerable = typeof(IEnumerable).IsAssignableFrom(applyMethod.ReturnType);
			var dynamicMethod = new DynamicMethod(string.Format("ApplyCommand_{0}_{1}", aggregateRootType.Name, commandType.Name), enumerable ? typeof(IEnumerable) : typeof(object), new Type[] { typeof(object), typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, aggregateRootType);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			ilGenerator.Emit(OpCodes.Castclass, commandType);
			ilGenerator.EmitCall(OpCodes.Callvirt, applyMethod, null);
			ilGenerator.Emit(OpCodes.Ret);

			return dynamicMethod.CreateDelegate(enumerable ? typeof(AggregateRootProxy.ApplyEnumerableCommandDelegate) : typeof(AggregateRootProxy.ApplyObjectCommandDelegate));
		}

		/// <summary>
		/// Creates a delegate that will get the Aggregate Root Id from a message.
		/// </summary>
		/// <param name="messageType">The <see cref="Type"/> of the message.</param>
		/// <param name="property">The name of the property that will provide the Aggregate Root Id</param>
		/// <returns>A <see cref="MessageProxy.GetAggregateRootIdsDelegate"/> delegate</returns>
		public static MessageProxy.GetAggregateRootIdsDelegate CreateGetAggregateRootIdDelegate(Type messageType, PropertyInfo property)
		{
			var dynamicMethod = new DynamicMethod("GetAggregateRootId_" + messageType.Name, typeof(IEnumerable<Guid>), new Type[] { typeof(object) });
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

			return (MessageProxy.GetAggregateRootIdsDelegate)dynamicMethod.CreateDelegate(typeof(MessageProxy.GetAggregateRootIdsDelegate));
		}

		/// <summary>
		/// Creates a delegate that will get Aggregate Root Ids from a message.
		/// </summary>
		/// <param name="messageType">The <see cref="Type"/> of the message.</param>
		/// <param name="property">The name of the property that will provide the Aggregate Root Ids</param>
		/// <returns>A <see cref="MessageProxy.GetAggregateRootIdsDelegate"/> delegate</returns>
		public static MessageProxy.GetAggregateRootIdsDelegate CreateGetAggregateRootIdsDelegate(Type messageType, PropertyInfo property)
		{
			var dynamicMethod = new DynamicMethod("GetAggregateRootIds_" + messageType.Name, typeof(IEnumerable<Guid>), new Type[] { typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, messageType);
			ilGenerator.EmitCall(OpCodes.Callvirt, property.GetGetMethod(), null);
			ilGenerator.Emit(OpCodes.Castclass, typeof(IEnumerable<Guid>));
			ilGenerator.Emit(OpCodes.Ret);

			return (MessageProxy.GetAggregateRootIdsDelegate)dynamicMethod.CreateDelegate(typeof(MessageProxy.GetAggregateRootIdsDelegate));
		}
	}
}
