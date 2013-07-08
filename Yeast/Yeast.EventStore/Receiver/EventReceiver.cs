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
		private Dictionary<Type, Type> CommandTypeToAggregateRootType = new Dictionary<Type, Type>();
		private delegate IAggregateRoot CreateAndLoadAggregateRoot(Guid aggregateId);
		private static Dictionary<Type, CreateAndLoadAggregateRoot> CreateAndLoadAggregateRootDelegates = new Dictionary<Type, CreateAndLoadAggregateRoot>();

		public void Receive(object command)
		{
			var commandType = command.GetType();
			Type aggregateType;
			if (!CommandTypeToAggregateRootType.TryGetValue(commandType, out aggregateType))
			{
				throw new RegistrationException("No AggregateBase Type registered for command type " + commandType.Name) { CommandType = commandType };
			}

			var getCreateAndLoadAggregateRoot = GetCreateAndLoadAggregateRootDelegates(aggregateType);
			getCreateAndLoadAggregateRoot((command as ICommand).AggregateRootId)
				.HandleCommand(command);
		}

		private static CreateAndLoadAggregateRoot GetCreateAndLoadAggregateRootDelegates(Type aggregateType)
		{
			if (!CreateAndLoadAggregateRootDelegates.ContainsKey(aggregateType))
			{
				var guidConstructor = aggregateType.GetConstructor(new Type[] { typeof(Guid) });
				var dynamicMethod = new DynamicMethod("GuidConstructor_" + aggregateType.Name, aggregateType, new Type[] { typeof(Guid) });
				var ilGenerator = dynamicMethod.GetILGenerator();

				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Newobj, guidConstructor);
				ilGenerator.Emit(OpCodes.Ret);

				lock (CreateAndLoadAggregateRootDelegates)
				{
					CreateAndLoadAggregateRootDelegates[aggregateType] = (CreateAndLoadAggregateRoot)dynamicMethod.CreateDelegate(typeof(CreateAndLoadAggregateRoot));
				}
			}

			return CreateAndLoadAggregateRootDelegates[aggregateType];
		}

		public IEventReceiver Register<AR, C>()
			where AR : IAggregateRoot
			where C : ICommand
		{
			CommandTypeToAggregateRootType[typeof(C)] = typeof(AR);
			return this;
		}
	}
}
