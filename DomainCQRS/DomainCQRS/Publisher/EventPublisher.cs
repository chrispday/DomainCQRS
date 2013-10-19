using System;
using System.Collections.Generic;
using System.Threading;

using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class EventPublisherConfigure
	{
		public static string DefaultSubscriberReceiveMethodName = "Receive";

		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId) { return Subscribe<Subscriber, object>(configure, subscriptionId); }
		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId) where Event : class { return Subscribe<Subscriber, Event>(configure, subscriptionId, DefaultSubscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId, string subscriberReceiveMethodName) where Event : class { return Subscribe<Subscriber, Event>(configure, subscriptionId, Activator.CreateInstance<Subscriber>(), subscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriber); }
		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriber, subscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber) where Event : class { return Subscribe<Subscriber, Event>(configure, subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName)
			where Event : class
		{
			configure.EventPublisher.Subscribe<Subscriber, Event>(subscriptionId, subscriber, subscriberReceiveMethodName);
			return configure;
		}
	}

	public delegate void Receive(object subscriber, object @event);

	public abstract class EventPublisherBase : IEventPublisher
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly IEventStore _eventStore;
		public IEventStore EventStore { get { return _eventStore; } }
		private readonly IMessageSender _sender;
		public IMessageSender Sender { get { return _sender; } }
		private readonly string _defaultSubscriberReceiveMethodName;
		public string DefaultSubscriberReceiveMethodName { get { return _defaultSubscriberReceiveMethodName; } }

		protected class SubscriberAndPosition
		{
			public object Subscriber;
			public Dictionary<Type, Receive> Receives = new Dictionary<Type, Receive>();
			public Receive ReceiveObject;
			public IEventPersisterPosition Position;
		}
		protected Dictionary<Guid, SubscriberAndPosition> _subscribers = new Dictionary<Guid, SubscriberAndPosition>();

		public EventPublisherBase(ILogger logger, IEventStore eventStore, IMessageSender sender, string defaultSubscriberReceiveMethodName)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == eventStore)
			{
				throw new ArgumentNullException("eventStore");
			}
			if (null == sender)
			{
				throw new ArgumentNullException("sender");
			}
			if (null == defaultSubscriberReceiveMethodName)
			{
				throw new ArgumentNullException("defaultSubscriberReceiveMethodName");
			}

			_logger = logger;
			_eventStore = eventStore;
			_sender = sender;
			_defaultSubscriberReceiveMethodName = defaultSubscriberReceiveMethodName;
		}

		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId) { return Subscribe<Subscriber, object>(subscriptionId, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(subscriptionId, subscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId) where Event : class { return Subscribe<Subscriber, Event>(subscriptionId, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, string subscriberReceiveMethodName) where Event : class { return Subscribe<Subscriber, Event>(subscriptionId, Activator.CreateInstance<Subscriber>(), DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, object>(subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber) where Event : class { return Subscribe<Subscriber, Event>(subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName)
			where Event : class
		{
			SubscriberAndPosition subscriberAndPosition;
			if (!_subscribers.TryGetValue(subscriptionId, out subscriberAndPosition))
			{
				_subscribers.Add(subscriptionId, subscriberAndPosition = new SubscriberAndPosition() { Subscriber = subscriber, Position = EventStore.EventStoreProvider.LoadPosition(subscriptionId) });
			}
			if (typeof(object) == typeof(Event))
			{
				subscriberAndPosition.ReceiveObject = ILHelper.CreateReceive<Subscriber, object>(subscriberReceiveMethodName);
			}
			else
			{
				var eventType = typeof(Event);
				if (subscriberAndPosition.Receives.ContainsKey(eventType))
				{
					throw new RegistrationException(string.Format("{0}({1}) for {2} already registered.", subscriberReceiveMethodName, eventType.Name, subscriptionId));
				}
				subscriberAndPosition.Receives.Add(eventType, ILHelper.CreateReceive<Subscriber, Event>(subscriberReceiveMethodName));
			}

			return this;
		}

		public object GetSubscriber(Guid subscriptionId)
		{
			return GetSubscriber<object>(subscriptionId);
		}

		public Subscriber GetSubscriber<Subscriber>(Guid subscriptionId)
			where Subscriber : class
		{
			return _subscribers[subscriptionId].Subscriber as Subscriber;
		}

		public abstract void Dispose();
	}
}
