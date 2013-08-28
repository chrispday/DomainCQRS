using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Yeast.EventStore.Common
{
	public static class ILHelper
	{
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

		public static Receive CreateReceive<Subscriber, Event>(string subscriberReceiveMethodName)
		{
			var subscriberType = typeof(Subscriber);
			var eventType = typeof(Event);
			MethodInfo receiveMethod = subscriberType.GetMethod(subscriberReceiveMethodName, new Type[] { eventType });
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

		public static CreateAggreateRoot CreateCreateAggreateRoot(Type aggregateRootType, CreateAggreateRoot createAggregateRoot)
		{
			var dynamicMethod = new DynamicMethod(aggregateRootType.Name + "_Create", typeof(object), null);
			var ilGenerator = dynamicMethod.GetILGenerator();
			ilGenerator.Emit(OpCodes.Newobj, aggregateRootType.GetConstructor(Type.EmptyTypes));
			ilGenerator.Emit(OpCodes.Ret);
			createAggregateRoot = (CreateAggreateRoot)dynamicMethod.CreateDelegate(typeof(CreateAggreateRoot));
			return createAggregateRoot;
		}

		public static ApplyEvent CreateApplyEvent(Type eventType, Type aggregateRootType)
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

			return (ApplyEvent)dynamicMethod.CreateDelegate(typeof(ApplyEvent));
		}

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

			return dynamicMethod.CreateDelegate(enumerable ? typeof(ApplyEnumerableCommand) : typeof(ApplyObjectCommand));
		}

		public static GetAggregateRootIds CreateGetAggregateRootIdDelegate(Type messageType, PropertyInfo property)
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

		public static GetAggregateRootIds CreateGetAggregateRootIdsDelegate(Type messageType, PropertyInfo property)
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
	}
}
