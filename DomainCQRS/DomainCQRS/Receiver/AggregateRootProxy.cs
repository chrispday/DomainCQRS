using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Provides a proxy for Aggregate Root types.  The proxy is used to cache delegate calls to methods.
	/// </summary>
	public class AggregateRootProxy : IAggregateRootProxy
	{
		/// <summary>
		/// The aggregate root type to proxy
		/// </summary>
		public AggregateRootProxy(Type type)
		{
			if (null == type)
			{
				throw new ArgumentNullException("type");
			}

			_type = type;
			CreateCreate();
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

		/// <summary>
		/// Create an object using the empty constructor.
		/// </summary>
		/// <returns></returns>
		public object Create()
		{
			return _createAggreateRoot();
		}

		/// <summary>
		/// Applies a command to the aggregate root.
		/// </summary>
		/// <param name="aggregateRoot">The aggregate root to apply the command to.</param>
		/// <param name="command">The command to apply.</param>
		/// <returns>The events generated from the aggregate root applying the command.</returns>
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

		/// <summary>
		/// Applies a historical event to the aggregate root.
		/// </summary>
		/// <param name="aggregateRoot">The aggregate root.</param>
		/// <param name="event">The event to apply.</param>
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

		/// <summary>
		/// Register a command (message) that can be applied to the aggregate root type.
		/// </summary>
		/// <param name="messageProxy">The <see cref="IMessageProxy"/> for the command type.</param>
		/// <param name="aggregateRootApplyMethod">The name of the method that will apply the message type.</param>
		/// <returns>The <see cref="IAggregateRootProxy"/>.</returns>
		public IAggregateRootProxy Register(IMessageProxy messageProxy, string aggregateRootApplyMethod)
		{
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
			_createAggreateRoot = ILHelper.CreateCreateAggreateRoot(Type);
		}

	}

	public static class AggregateRootProxyExtensions
	{
		/// <summary>
		/// Creates a new aggregate root type proxy.
		/// </summary>
		/// <param name="aggregateRootType">The type of the aggregate root.</param>
		/// <returns>A new <see cref="IAggregateRootProxy"/>.</returns>
		public static IAggregateRootProxy CreateAggregateRootProxy(this Type aggregateRootType)
		{
			return new AggregateRootProxy(aggregateRootType);
		}
	}
}
