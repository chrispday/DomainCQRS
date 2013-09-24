using System;
using System.Collections.Generic;
using System.Threading;

using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class EventPublisherConfigure
	{
		public static int DefaultBatchSize = 10000;
		public static TimeSpan DefaultPublishThreadSleep = TimeSpan.FromSeconds(1);
		public static string DefaultSubscriberReceiveMethodName = "Receive";

		public static IConfigure EventPublisher(this IConfigure configure) { return configure.EventPublisher(DefaultBatchSize); }
		public static IConfigure EventPublisher(this IConfigure configure, int batchSize)
		{
			configure.Registry
				.BuildInstancesOf<IEventPublisher>()
				.TheDefaultIs(Registry.Instance<IEventPublisher>()
					.UsingConcreteType<EventPublisher>()
					.WithProperty("batchSize").EqualTo(batchSize)
					.WithProperty("publishThreadSleep").EqualTo(DefaultPublishThreadSleep.Ticks)
					.WithProperty("defaultSubscriberReceiveMethodName").EqualTo(DefaultSubscriberReceiveMethodName))
				.AsSingletons();
			return configure;
		}

		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId) { return Subscribe<Subscriber, object>(configure, subscriptionId); }
		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId) { return Subscribe<Subscriber, Event>(configure, subscriptionId, DefaultSubscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, Event>(configure, subscriptionId, Activator.CreateInstance<Subscriber>(), subscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriber); }
		public static IBuiltConfigure Subscribe<Subscriber>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriber, subscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, Event>(configure, subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public static IBuiltConfigure Subscribe<Subscriber, Event>(this IBuiltConfigure configure, Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName)
		{
			configure.EventPublisher.Subscribe<Subscriber, Event>(subscriptionId, subscriber, subscriberReceiveMethodName);
			return configure;
		}
	}

	public delegate void Receive(object subscriber, object @event);

	public class EventPublisher : IEventPublisher
	{
		private readonly ILogger _logger;
		public ILogger Logger { get { return _logger; } }
		private readonly IEventStore _eventStore;
		public IEventStore EventStore { get { return _eventStore; } }
		private readonly int _batchSize;
		public int BatchSize { get { return _batchSize; } }
		private readonly TimeSpan _publishThreadSleep;
		public TimeSpan PublishThreadSleep { get { return _publishThreadSleep; } }
		private readonly string _defaultSubscriberReceiveMethodName;
		public string DefaultSubscriberReceiveMethodName { get { return _defaultSubscriberReceiveMethodName; } }
		public IMessageReceiver MessageReceiver { get; set; }
		private bool _synchronous = false;
		public bool Synchronous
		{
			get { return _synchronous; }
			set
			{
				_synchronous = value;
				if (value)
				{
					StopPublishingThread();
				}
				else
				{
					StartPublishingThread();
				}
			}
		}

		protected class SubscriberAndPosition
		{
			public object Subscriber;
			public Dictionary<Type, Receive> Receives = new Dictionary<Type, Receive>();
			public Receive ReceiveObject;
			public IEventStoreProviderPosition Position;
		}
		protected Dictionary<Guid, SubscriberAndPosition> _subscribers = new Dictionary<Guid, SubscriberAndPosition>();
		private Thread _publisherThread;
		private Dictionary<Guid, Thread> _subscriptionThreads = new Dictionary<Guid, Thread>();
		private volatile bool _continuePublishing = true;
		private AutoResetEvent _finishedPublishing = new AutoResetEvent(false);

		public EventPublisher(ILogger logger, IEventStore eventStore, int batchSize, long publishThreadSleep, string defaultSubscriberReceiveMethodName)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == eventStore)
			{
				throw new ArgumentNullException("eventStore");
			}
			if (0 >= batchSize)
			{
				throw new ArgumentOutOfRangeException("batchSize");
			}
			if (0 >= publishThreadSleep)
			{
				throw new ArgumentOutOfRangeException("publishThreadSleep");
			}
			if (null == defaultSubscriberReceiveMethodName)
			{
				throw new ArgumentNullException("defaultSubscriberReceiveMethodName");
			}

			_logger = logger;
			_eventStore = eventStore;
			_batchSize = batchSize;
			_publishThreadSleep = TimeSpan.FromTicks(publishThreadSleep);
			_defaultSubscriberReceiveMethodName = defaultSubscriberReceiveMethodName;
		}

		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId) { return Subscribe<Subscriber, object>(subscriptionId, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(subscriptionId, subscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId) { return Subscribe<Subscriber, Event>(subscriptionId, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, Event>(subscriptionId, Activator.CreateInstance<Subscriber>(), DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, object>(subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, Event>(subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName)
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

			if (!Synchronous
				&& null == _publisherThread)
			{
				StartPublishingThread();
			}

			return this;
		}

		public void Dispose()
		{
			StopPublishingThread();
		}

		private void StartPublishingThread()
		{
			lock (this)
			{
				if (null == _publisherThread)
				{
					_continuePublishing = true;
					_finishedPublishing.Reset();
					_publisherThread = new ThreadStart(Publish).Start("EventPublisher");
				}
			}
		}

		private void StopPublishingThread()
		{
			lock (this)
			{
				if (null != _publisherThread)
				{
					_publisherThread = null;
					_continuePublishing = false;
					_finishedPublishing.WaitOne();
				}
			}
		}

		private void Publish()
		{
			Logger.Information("Starting publishing thread.");

			while (_continuePublishing)
			{
				Logger.Verbose("Next publish run.");

				foreach (var subscription in new Dictionary<Guid, SubscriberAndPosition>(_subscribers))
				{
					lock (_subscriptionThreads)
					{
						if (_subscriptionThreads.ContainsKey(subscription.Key))
						{
							Logger.Warning("Skipped publishing for {0}, still waiting for previous thread to finish.", subscription.Key);
							continue;
						}

						_subscriptionThreads.Add(subscription.Key, new Action<KeyValuePair<Guid, SubscriberAndPosition>>(PublishForSubscription).Start("Subscription" + subscription.Key.ToString(), subscription));
					}
				}

				Thread.Sleep(PublishThreadSleep);
			}

			Logger.Information("Shutting down publishing thread.");
			_finishedPublishing.Set();
		}

		public void Publish(object @event)
		{
			if (!Synchronous)
			{
				throw new InvalidOperationException("Not publishing synchronously.");
			}

			var subscribers = new List<SubscriberAndPosition>(_subscribers.Values);
			foreach (var subscriber in subscribers)
			{
				Receive receive = subscriber.ReceiveObject;
				if (null == receive)
				{
					subscriber.Receives.TryGetValue(@event.GetType(), out receive);
				}

				if (null != receive)
				{
					receive(subscriber.Subscriber, @event);
				}
			}
		}

		private void PublishForSubscription(KeyValuePair<Guid, SubscriberAndPosition> subscription)
		{
			try
			{
				Logger.Verbose("Publishing for {0} {1}.", subscription.Value.Subscriber, subscription.Key);

				int eventsPublished = 0;

				if (null == subscription.Value.Position)
				{
					subscription.Value.Position = EventStore.CreateEventStoreProviderPosition();
				}

				var to = EventStore.CreateEventStoreProviderPosition();
				foreach (var @event in EventStore.Load(BatchSize, subscription.Value.Position, to))
				{
					Receive receive;
					if (0 == subscription.Value.Receives.Count
						|| !subscription.Value.Receives.TryGetValue(@event.Event.GetType(), out receive))
					{
						receive = subscription.Value.ReceiveObject;
					}

					if (null != receive)
					{
						receive(subscription.Value.Subscriber, @event.Event);
					}

					eventsPublished++;

					if (!_continuePublishing)
					{
						break;
					}
				}

				EventStore.EventStoreProvider.SavePosition(subscription.Key, subscription.Value.Position = to);

				Logger.Verbose("{0} events published for {1}.", eventsPublished, subscription.Key);
			}
			catch (Exception ex)
			{
				Logger.Error("{0}", ex);
			}
			finally
			{
				lock (_subscriptionThreads)
				{
					_subscriptionThreads.Remove(subscription.Key);
				}
			}
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
	}
}
