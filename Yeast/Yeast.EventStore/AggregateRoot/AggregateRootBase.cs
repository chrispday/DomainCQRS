using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace Yeast.EventStore
{
	public abstract class AggregateRootBase : IAggregateRoot
	{
		public Guid AggregateId { get; set; }
		public int Version { get; set; }

		public AggregateRootBase()
		{
			AggregateId = Guid.NewGuid();
			Version = -1;
		}

		public AggregateRootBase(Guid aggregateId)
		{
			AggregateId = aggregateId;
			Version = -1;
			Load();
		}

		public virtual IAggregateRoot HandleCommand(object command)
		{
			return Save(command).ApplyCommand(command);
		}

		#region EventStore Related

		protected virtual AggregateRootBase Save(object command)
		{
			EventStore.Current.Save(AggregateId, ++Version, command);
			return this;
		}

		protected virtual AggregateRootBase Load()
		{
			foreach (var storedEvent in EventStore.Current.Load(AggregateId, null, null, null, null))
			{
				Version = storedEvent.Version;
				ApplyCommand(storedEvent.Event);
			}

			return this;
		}

		#endregion

		#region Automagic Command Handling

		protected virtual AggregateRootBase ApplyCommand(object command)
		{
			GetApplyCommand(GetType(), command.GetType()).Invoke(this, command);
			return this;
		}

		protected delegate void ApplyCommandInvoker(object aggregateRoot, object command);
		protected struct AggregateRootAndCommandType { public Type AggregateRootType; public Type CommandType; }
		protected static Dictionary<AggregateRootAndCommandType, ApplyCommandInvoker> ApplyCommandInvokers = new Dictionary<AggregateRootAndCommandType, ApplyCommandInvoker>();
		protected static ApplyCommandInvoker GetApplyCommand(Type aggregateRootType, Type commandType)
		{
			var key = new AggregateRootAndCommandType() { AggregateRootType = aggregateRootType, CommandType = commandType };

			if (!ApplyCommandInvokers.ContainsKey(key))
			{
				var targetMethod = aggregateRootType.GetMethod("Handle", new Type[] { commandType });
				if (null == targetMethod)
				{
					throw new CommandHandlerException(string.Format("{0} does not handle {1}.", aggregateRootType.Name, commandType.Name)) { AggregateType = aggregateRootType, CommandType = commandType };
				}

				var dynamicMethod = new DynamicMethod(string.Format("ApplyCommand_{0}_{1}", aggregateRootType.Name, commandType.Name), null, new Type[] { typeof(object), typeof(object) });
				var ilGenerator = dynamicMethod.GetILGenerator();

				ilGenerator.Emit(OpCodes.Nop);
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Castclass, aggregateRootType);
				ilGenerator.Emit(OpCodes.Ldarg_1);
				ilGenerator.Emit(OpCodes.Castclass, commandType);
				ilGenerator.EmitCall(OpCodes.Callvirt, targetMethod, null);
				ilGenerator.Emit(OpCodes.Nop);
				ilGenerator.Emit(OpCodes.Ret);

				lock (ApplyCommandInvokers)
				{
					ApplyCommandInvokers[key] = (ApplyCommandInvoker)dynamicMethod.CreateDelegate(typeof(ApplyCommandInvoker));
				}
			}

			return ApplyCommandInvokers[key];
		}

		#endregion
	}

	public abstract class AggregateRootBase<T> : AggregateRootBase, IAggregateRoot<T>
	{
		public AggregateRootBase() : base() { }
		public AggregateRootBase(Guid aggregateId) : base(aggregateId) { }

		public new T HandleCommand(object command)
		{
			return (T)base.HandleCommand(command);
		}
	}
}
