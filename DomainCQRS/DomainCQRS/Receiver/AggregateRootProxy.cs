using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public class AggregateRootProxy : IAggregateRootProxy
	{
		public AggregateRootProxy(Type type)
		{
			if (null == type)
			{
				throw new ArgumentNullException("type");
			}

			_type = type;
		}

		public delegate object CreateAggreateRootDelegate();
		public delegate IEnumerable ApplyEnumerableCommandDelegate(object aggregateRoot, object command);
		public delegate object ApplyObjectCommandDelegate(object aggregateRoot, object command);
		public delegate void ApplyEventDelegate(object aggregateRoot, object @event);

		private CreateAggreateRootDelegate _createAggreateRoot;
		private Dictionary<Type, Delegate> _commandApplies = new Dictionary<Type, Delegate>();
		private Dictionary<Type, ApplyEventDelegate> _eventApplies = new Dictionary<Type, ApplyEventDelegate>();

		private readonly Type _type;
		public Type Type { get { return _type; } }

		public object Create()
		{
			return _createAggreateRoot();
		}

		public IEnumerable ApplyCommand(object aggregateRoot, object command)
		{
			var apply = _commandApplies[command.GetType()];
			var applyObject = apply as ApplyObjectCommandDelegate;
			if (null != applyObject)
			{
				return new object[] { applyObject(aggregateRoot, command) };
			}
			return (apply as ApplyEnumerableCommandDelegate)(aggregateRoot, command);
		}

		public void ApplyEvent(object aggregateRoot, object @event)
		{
			var eventType = @event.GetType();

			ApplyEventDelegate applyEvent;
			if (!_eventApplies.TryGetValue(eventType, out applyEvent))
			{
				_eventApplies[eventType] = applyEvent = ILHelper.CreateApplyEvent(eventType, Type);
			}

			applyEvent(aggregateRoot, @event);
		}

		public IAggregateRootProxy Register(IMessageProxy messageProxy, string aggregateRootApplyMethod)
		{
			if (null == _createAggreateRoot)
			{
				CreateCreate();
			}
			CreateCommandApply(messageProxy, aggregateRootApplyMethod);

			return this;
		}

		private void CreateCommandApply(IMessageProxy messageProxy, string aggregateRootApplyMethod)
		{
			if (null == messageProxy)
			{
				throw new ArgumentNullException("messageProxy");
			}
			if (string.IsNullOrEmpty(aggregateRootApplyMethod))
			{
				throw new ArgumentNullException("aggregateRootApplyMethod");
			}

			if (_commandApplies.ContainsKey(messageProxy.Type))
			{
				throw new RegistrationException(string.Format("{0} has already been registered for {1}", messageProxy.Type, Type));
			}

			var applyMethod = Type.GetMethod(aggregateRootApplyMethod, new Type[] { messageProxy.Type }, true);
			if (null == applyMethod
				|| typeof(object) == applyMethod.GetParameters()[0].ParameterType)
			{
				throw new RegistrationException(string.Format("{2} does not contain method {1}({0})", messageProxy.Type, aggregateRootApplyMethod, Type));
			}

			_commandApplies[messageProxy.Type] = ILHelper.CreateApplyCommand(messageProxy.Type, Type, applyMethod);
		}

		private void CreateCreate()
		{
			var constructor = Type.GetConstructor(Type.EmptyTypes);
			if (null == constructor)
			{
				throw new RegistrationException(string.Format("{0} does not have an empty constructor.", Type));
			}
			_createAggreateRoot = Common.ILHelper.CreateCreateAggreateRoot(Type);
		}

	}

	public static class AggregateRootProxyExtensions
	{
		public static IAggregateRootProxy CreateAggregateRootProxy(this Type aggregateRootType)
		{
			return new AggregateRootProxy(aggregateRootType);
		}
	}
}
