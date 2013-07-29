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
		private Dictionary<Guid, IEventSubscriber> _subscribers = new Dictionary<Guid, IEventSubscriber>();
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
			_subscribers.Add(subscriptionId, new Subscriber());

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

			IEventStoreProviderPosition from = EventStore.CreateEventStoreProviderPosition();
			IEventStoreProviderPosition to = EventStore.CreateEventStoreProviderPosition();

			while (_continuePublishing)
			{
				Logger.Verbose("Next publish run.");

				foreach (var subscription in _subscribers)
				{
					Logger.Verbose("Publishing for {0} {1}.", subscription.Value.GetType().Name, subscription.Key);
					try
					{
						foreach (var @event in EventStore.Load(BatchSize, from, to))
						{
							subscription.Value.Receive(@event);
						}

						from = to;
						to = EventStore.CreateEventStoreProviderPosition();
					}
					catch (Exception ex)
					{
						Logger.Error("{0}", ex);
					}
				}
				Thread.Sleep(100);
			}

			Logger.Information("Shutting down publishing thread.");
			_finishedPublishing.Set();
		}
	}
}
