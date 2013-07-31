using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Yeast.EventStore
{
	public static class EventPublisherConfigure
	{
		public static IConfigure EventPublisher(this IConfigure configure)
		{
			var c = configure as Configure;
			c.EventPublisher = new EventPublisher() { Logger = c.Logger, EventStore = c.EventStore };
			return configure;
		}

		public static IConfigure EventPublisher(this IConfigure configure, int batchSize)
		{
			var c = configure as Configure;
			c.EventPublisher = new EventPublisher() { Logger = c.Logger, EventStore = c.EventStore, BatchSize = batchSize };
			return configure;
		}

		public static IConfigure Subscribe<Subscriber>(this IConfigure configure, Guid subscriptionId)
			where Subscriber : IEventSubscriber, new()
		{
			var c = configure as Configure;
			c.EventPublisher.Subscribe<Subscriber>(subscriptionId);
			return configure;
		}
	}

	public class EventPublisher : IEventPublisher, IDisposable
	{
		public Common.ILogger Logger { get; set; }
		public IEventStore EventStore { get; set; }
		public int BatchSize { get; set; }
		protected class SubscriberAndPosition
		{
			public IEventSubscriber Subscriber;
			public IEventStoreProviderPosition Position;
		}
		protected Dictionary<Guid, SubscriberAndPosition> _subscribers = new Dictionary<Guid, SubscriberAndPosition>();
		private Thread _publisherThread;
		private volatile bool _continuePublishing = true;
		private AutoResetEvent _finishedPublishing = new AutoResetEvent(false);

		public EventPublisher()
		{
			BatchSize = 100;
		}

		public IEventPublisher Subscribe<Subscriber>(Guid subscriptionId)
			where Subscriber : IEventSubscriber, new()
		{
			_subscribers.Add(subscriptionId, new SubscriberAndPosition() { Subscriber = new Subscriber() });

			if (null == _publisherThread)
			{
				_publisherThread = new Thread(Publish);
				_publisherThread.Start();
			}

			return this;
		}

		public void Dispose()
		{
			_continuePublishing = false;
			_finishedPublishing.WaitOne();
		}

		private void Publish()
		{
			Logger.Information("Starting publishing thread.");

			while (_continuePublishing)
			{
				Logger.Verbose("Next publish run.");

				foreach (var subscription in new Dictionary<Guid,SubscriberAndPosition>(_subscribers))
				{
					try
					{
						Logger.Verbose("Publishing for {0} {1}.", subscription.Value.Subscriber.GetType().Name, subscription.Key);

						if (null == subscription.Value.Position)
						{
							subscription.Value.Position = EventStore.CreateEventStoreProviderPosition();
						}

						var to = EventStore.CreateEventStoreProviderPosition();
						foreach (var @event in EventStore.Load(BatchSize, subscription.Value.Position, to))
						{
							subscription.Value.Subscriber.Receive(@event);
						}

						subscription.Value.Position = to;
					}
					catch (Exception ex)
					{
						Logger.Error("{0}", ex);
					}
				}
				Thread.Sleep(750);
			}

			Logger.Information("Shutting down publishing thread.");
			_finishedPublishing.Set();
		}
	}
}
