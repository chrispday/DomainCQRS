using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Yeast.EventStore
{
	public abstract class AggregateRootBase : IAggregateRoot
	{
		public IEventStore EventStore { get; set; }
		public Guid Id { get; set; }
		public int Version { get; set; }

		public AggregateRootBase(IEventStore eventStore)
		{
			EventStore = eventStore;
			Id = Guid.NewGuid();
			Version = -1;
		}

		public AggregateRootBase(IEventStore eventStore, Guid id)
			: this(eventStore)
		{
			Id = id;
			Version = -1;
			Load();
		}

		public IEnumerable Apply(object command)
		{
			return Save(GetApply(GetType(), command.GetType()).Invoke(this, command));
		}

		public object When(object @event)
		{
			return GetWhen(GetType(), @event.GetType()).Invoke(this, @event);
		}

		#region EventStore Related

		protected virtual IEnumerable Save(IEnumerable events)
		{
			foreach (var @event in events)
			{
				EventStore.Save(Id, ++Version, When(@event));
			}
			return events;
		}

		protected virtual AggregateRootBase Load()
		{
			foreach (var storedEvent in EventStore.Load(Id, Version, null, null, null))
			{
				Version = storedEvent.Version;
				When(storedEvent.Event);
			}

			return this;
		}

		#endregion

		#region Automagic Command Handling

		protected struct AggregateRootTypeAndType { public Type AggregateRootType; public Type Type; }

		protected delegate IEnumerable ApplyDelegate(object aggregateRoot, object command);
		protected static Dictionary<AggregateRootTypeAndType, ApplyDelegate> ApplyDelegates = new Dictionary<AggregateRootTypeAndType, ApplyDelegate>();
		protected static ApplyDelegate GetApply(Type aggregateRootType, Type commandType)
		{
			var key = new AggregateRootTypeAndType() { AggregateRootType = aggregateRootType, Type = commandType };

			if (!ApplyDelegates.ContainsKey(key))
			{
				var targetMethod = aggregateRootType.GetMethod("Apply", new Type[] { commandType });
				if (null == targetMethod
					|| typeof(object) == targetMethod.GetParameters()[0].ParameterType)
				{
					throw new CommandApplyException(string.Format("{0} does not handle {1}.", aggregateRootType.Name, commandType.Name)) { AggregateType = aggregateRootType, CommandType = commandType };
				}

				var dynamicMethod = new DynamicMethod(string.Format("Apply_{0}_{1}", aggregateRootType.Name, commandType.Name), typeof(IEnumerable), new Type[] { typeof(object), typeof(object) });
				var ilGenerator = dynamicMethod.GetILGenerator();

				ilGenerator.Emit(OpCodes.Nop);
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Castclass, aggregateRootType);
				ilGenerator.Emit(OpCodes.Ldarg_1);
				ilGenerator.Emit(OpCodes.Castclass, commandType);
				ilGenerator.EmitCall(OpCodes.Callvirt, targetMethod, null);
				ilGenerator.Emit(OpCodes.Nop);
				ilGenerator.Emit(OpCodes.Ret);

				lock (ApplyDelegates)
				{
					ApplyDelegates[key] = (ApplyDelegate)dynamicMethod.CreateDelegate(typeof(ApplyDelegate));
				}
			}

			return ApplyDelegates[key];
		}

		protected delegate object WhenDelegate(object aggregateRoot, object @event);
		protected static Dictionary<AggregateRootTypeAndType, WhenDelegate> WhenDelegates = new Dictionary<AggregateRootTypeAndType, WhenDelegate>();
		protected static WhenDelegate GetWhen(Type aggregateRootType, Type eventType)
		{
			var key = new AggregateRootTypeAndType() { AggregateRootType = aggregateRootType, Type = eventType };

			if (!WhenDelegates.ContainsKey(key))
			{
				var targetMethod = aggregateRootType.GetMethod("When", new Type[] { eventType });
				if (null == targetMethod)
				{
					throw new EventWhenException(string.Format("{0} does not handle {1}.", aggregateRootType.Name, eventType.Name)) { AggregateType = aggregateRootType, EventType = eventType };
				}

				var dynamicMethod = new DynamicMethod(string.Format("When_{0}_{1}", aggregateRootType.Name, eventType.Name), typeof(object), new Type[] { typeof(object), typeof(object) });
				var ilGenerator = dynamicMethod.GetILGenerator();

				ilGenerator.Emit(OpCodes.Nop);
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Castclass, aggregateRootType);
				ilGenerator.Emit(OpCodes.Ldarg_1);
				ilGenerator.Emit(OpCodes.Castclass, eventType);
				ilGenerator.EmitCall(OpCodes.Callvirt, targetMethod, null);
				ilGenerator.Emit(OpCodes.Nop);
				ilGenerator.Emit(OpCodes.Ret);

				lock (WhenDelegates)
				{
					WhenDelegates[key] = (WhenDelegate)dynamicMethod.CreateDelegate(typeof(WhenDelegate));
				}
			}

			return WhenDelegates[key];
		}

		#endregion
	}
}
