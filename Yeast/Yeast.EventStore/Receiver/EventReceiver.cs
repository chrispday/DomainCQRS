using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace Yeast.EventStore
{
	public class EventReceiver : IEventReceiver
	{
		public IEventStore EventStore { get; set; }
		public string AggregateIdPropertyName { get; set; }
		public string VersionPropertyName { get; set; }

		public EventReceiver()
		{
			AggregateIdPropertyName = "AggregateRootId";
			VersionPropertyName = "Version";
		}

		public void Receive(object command)
		{
			var commandType = command.GetType();

			var aggregateRootId = GetGetAggregateIdDelegate(commandType, AggregateIdPropertyName)(command);
			var version = GetGetVersionDelegate(commandType, VersionPropertyName)(command);
			EventStore.Save(
				aggregateRootId,
				version,
				command);

			//Type aggregateType;
			//if (!CommandTypeToAggregateRootType.TryGetValue(commandType, out aggregateType))
			//{
			//	throw new RegistrationException("No AggregateBase Type registered for command type " + commandType.Name) { CommandType = commandType };
			//}

			//GetCreateAndLoadAggregateRootDelegates(aggregateType)
			//	(GetGetAggregateIdDelegate(commandType, AggregateIdPropertyName)(command))
			//	.HandleCommand(command);
		}

		//private Dictionary<Type, Type> CommandTypeToAggregateRootType = new Dictionary<Type, Type>();
		//public IEventReceiver Register<AggregateRoot, Command>()
		//{
		//	CommandTypeToAggregateRootType[typeof(Command)] = typeof(AggregateRoot);
		//	return this;
		//}

		#region Automagic Handling

		private delegate int GetVersion(object command);
		private static Dictionary<Type, GetVersion> GetVersionDelegates = new Dictionary<Type, GetVersion>();
		private static GetVersion GetGetVersionDelegate(Type commandType, string versionPropertyName)
		{
			GetVersion @delegate;
			if (!GetVersionDelegates.TryGetValue(commandType, out @delegate))
			{
				var dynamicMethod = new DynamicMethod("GetVersion_" + commandType.Name, typeof(int), new Type[] { typeof(object) });
				var ilGenerator = dynamicMethod.GetILGenerator();

				var versionPropertyGet = commandType.GetProperty(versionPropertyName).GetGetMethod();
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Castclass, commandType);
				ilGenerator.EmitCall(OpCodes.Callvirt, versionPropertyGet, null);
				ilGenerator.Emit(OpCodes.Ret);

				lock (GetVersionDelegates)
				{
					GetVersionDelegates[commandType] = @delegate = (GetVersion)dynamicMethod.CreateDelegate(typeof(GetVersion));
				}
			}

			return @delegate;
		}

		private delegate Guid GetAggregateRootId(object command);
		private static Dictionary<Type, GetAggregateRootId> GetAggregateIdDelegates = new Dictionary<Type, GetAggregateRootId>();
		private static GetAggregateRootId GetGetAggregateIdDelegate(Type commandType, string aggregateIdPropertyName)
		{
			GetAggregateRootId @delegate;
			if (!GetAggregateIdDelegates.TryGetValue(commandType, out @delegate))
			{
				var dynamicMethod = new DynamicMethod("GetAggregateId_" + commandType.Name, typeof(Guid), new Type[] { typeof(object) });
				var ilGenerator = dynamicMethod.GetILGenerator();

				var aggregateIdPropertyGet = commandType.GetProperty(aggregateIdPropertyName).GetGetMethod();
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Castclass, commandType);
				ilGenerator.EmitCall(OpCodes.Callvirt, aggregateIdPropertyGet, null);
				ilGenerator.Emit(OpCodes.Ret);

				lock (GetAggregateIdDelegates)
				{
					GetAggregateIdDelegates[commandType] = @delegate = (GetAggregateRootId)dynamicMethod.CreateDelegate(typeof(GetAggregateRootId));
				}
			}

			return @delegate;
		}

		private delegate IAggregateRoot CreateAndLoadAggregateRoot(Guid aggregateRootId);
		private static Dictionary<Type, CreateAndLoadAggregateRoot> CreateAndLoadAggregateRootDelegates = new Dictionary<Type, CreateAndLoadAggregateRoot>();
		private static CreateAndLoadAggregateRoot GetCreateAndLoadAggregateRootDelegates(Type aggregateType)
		{
			CreateAndLoadAggregateRoot @delegate;
			if (!CreateAndLoadAggregateRootDelegates.TryGetValue(aggregateType, out @delegate))
			{
				var guidConstructor = aggregateType.GetConstructor(new Type[] { typeof(Guid) });
				var dynamicMethod = new DynamicMethod("GuidConstructor_" + aggregateType.Name, aggregateType, new Type[] { typeof(Guid) });
				var ilGenerator = dynamicMethod.GetILGenerator();

				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Newobj, guidConstructor);
				ilGenerator.Emit(OpCodes.Ret);

				lock (CreateAndLoadAggregateRootDelegates)
				{
					CreateAndLoadAggregateRootDelegates[aggregateType] = @delegate = (CreateAndLoadAggregateRoot)dynamicMethod.CreateDelegate(typeof(CreateAndLoadAggregateRoot));
				}
			}

			return @delegate;
		}

		#endregion
	}
}
