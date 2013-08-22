using System;
using System.Collections.Generic;
using System.Threading;

using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public static class EventPublisherConfigure
	{
		public static int DefaultBatchSize = 100;
		public static TimeSpan DefaultPublishThreadSleep = TimeSpan.FromSeconds(1);
		public static string DefaultSubscriberReceiveMethodName = "Receive";

		public static IConfigure EventPublisher(this IConfigure configure) { return configure.EventPublisher(DefaultBatchSize); }
		public static IConfigure EventPublisher(this IConfigure configure, int batchSize)
		{
			var c = configure as Configure;
			c.EventPublisher = new EventPublisher() { Logger = c.Logger, EventStore = c.EventStore, BatchSize = batchSize, PublishThreadSleep = DefaultPublishThreadSleep };
			return configure;
		}

		public static IConfigure Subscribe<Subscriber>(this IConfigure configure, Guid subscriptionId) { return Subscribe<Subscriber, object>(configure, subscriptionId); }
		public static IConfigure Subscribe<Subscriber>(this IConfigure configure, Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriberReceiveMethodName); }
		public static IConfigure Subscribe<Subscriber, Event>(this IConfigure configure, Guid subscriptionId) { return Subscribe<Subscriber, Event>(configure, subscriptionId, DefaultSubscriberReceiveMethodName); }
		public static IConfigure Subscribe<Subscriber, Event>(this IConfigure configure, Guid subscriptionId, string subscriberReceiveMethodName) { return Subscribe<Subscriber, Event>(configure, subscriptionId, Activator.CreateInstance<Subscriber>(), subscriberReceiveMethodName); }

		public static IConfigure Subscribe<Subscriber>(this IConfigure configure, Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriber); }
		public static IConfigure Subscribe<Subscriber>(this IConfigure configure, Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName) { return Subscribe<Subscriber, object>(configure, subscriptionId, subscriber, subscriberReceiveMethodName); }
		public static IConfigure Subscribe<Subscriber, Event>(this IConfigure configure, Guid subscriptionId, Subscriber subscriber) { return Subscribe<Subscriber, Event>(configure, subscriptionId, subscriber, DefaultSubscriberReceiveMethodName); }
		public static IConfigure Subscribe<Subscriber, Event>(this IConfigure configure, Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName)
		{
			var c = configure as Configure;
			c.EventPublisher.Subscribe<Subscriber, Event>(subscriptionId, subscriber, subscriberReceiveMethodName);
			return configure;
		}
	}

	public delegate void Receive(object subscriber, object @event);

	public class EventPublisher : IEventPublisher
	{
		public Common.ILogger Logger { get; set; }
		public IEventStore EventStore { get; set; }
		public IMessageReceiver MessageReceiver { get; set; }
		public int BatchSize { get; set; }
		public TimeSpan PublishThreadSleep { get; set; }
		public string DefaultSubscriberReceiveMethodName { get; set; }
		protected class SubscriberAndPosition
		{
			public object Subscriber;
			public Dictionary<Type, Receive> Receives = new Dictionary<Type,Receive>();
			public Receive ReceiveObject;
			public IEventStoreProviderPosition Position;
		}
		protected Dictionary<Guid, SubscriberAndPosition> _subscribers = new Dictionary<Guid, SubscriberAndPosition>();
		private Thread _publisherThread;
		private Dictionary<Guid, Thread> _subscriptionThreads = new Dictionary<Guid, Thread>();
		private volatile bool _continuePublishing = true;
		private AutoResetEvent _finishedPublishing = new AutoResetEvent(false);

		public EventPublisher()
		{
			BatchSize = EventPublisherConfigure.DefaultBatchSize;
			PublishThreadSleep = EventPublisherConfigure.DefaultPublishThreadSleep;
			DefaultSubscriberReceiveMethodName = EventPublisherConfigure.DefaultSubscriberReceiveMethodName;

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

			if (null == _publisherThread)
			{
				_publisherThread = new ThreadStart(Publish).Start("EventPublisher");
			}

			return this;
		}

		public void Dispose()
		{
			if (null != _publisherThread)
			{
				_publisherThread = null;
				_continuePublishing = false;
				_finishedPublishing.WaitOne();
			}
		}

		private void Publish()
		{
			Logger.Information("Starting publishing thread.");

			while (_continuePublishing)
			{
				Logger.Verbose("Next publish run.");

				foreach (var subscription in new Dictionary<Guid,SubscriberAndPosition>(_subscribers))
				{
					if (_subscriptionThreads.ContainsKey(subscription.Key))
					{
						Logger.Warning("Skipped publishing for {0}, still waiting for previous thread to finish.", subscription.Key);
						continue;
					}

					_subscriptionThreads[subscription.Key] = new Action<KeyValuePair<Guid, SubscriberAndPosition>>(PublishForSubscription).Start("Subscription" + subscription.Key.ToString(), subscription);
				}

				Thread.Sleep(PublishThreadSleep);
			}

			Logger.Information("Shutting down publishing thread.");
			_finishedPublishing.Set();
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
				_subscriptionThreads.Remove(subscription.Key);
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
