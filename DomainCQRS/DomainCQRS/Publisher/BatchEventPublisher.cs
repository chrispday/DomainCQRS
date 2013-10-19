using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class BatchEventPublisherConfigure
	{
		public static int DefaultBatchSize = 10000;
		public static TimeSpan DefaultPublishThreadSleep = TimeSpan.FromSeconds(1);

		public static IConfigure BatchEventPublisher(this IConfigure configure) { return configure.BatchEventPublisher(DefaultBatchSize); }
		public static IConfigure BatchEventPublisher(this IConfigure configure, int batchSize)
		{
			configure.Registry
				.BuildInstancesOf<IEventPublisher>()
				.TheDefaultIs(Registry.Instance<IEventPublisher>()
					.UsingConcreteType<BatchEventPublisher>()
					.WithProperty("batchSize").EqualTo(batchSize)
					.WithProperty("publishThreadSleep").EqualTo(DefaultPublishThreadSleep.Ticks)
					.WithProperty("defaultSubscriberReceiveMethodName").EqualTo(EventPublisherConfigure.DefaultSubscriberReceiveMethodName))
				.AsSingletons();
			return configure;
		}
	}

	public class BatchEventPublisher : EventPublisherBase, IEventPublisher
	{
		private readonly int _batchSize;
		public int BatchSize { get { return _batchSize; } }
		private readonly TimeSpan _publishThreadSleep;
		public TimeSpan PublishThreadSleep { get { return _publishThreadSleep; } }

		private Thread _publisherThread;
		private Dictionary<Guid, Thread> _subscriptionThreads = new Dictionary<Guid, Thread>();
		private volatile bool _continuePublishing = true;
		private AutoResetEvent _finishedPublishing = new AutoResetEvent(false);

		public BatchEventPublisher(ILogger logger, IEventStore eventStore, IMessageSender sender, string defaultSubscriberReceiveMethodName, int batchSize, long publishThreadSleep)
			: base(logger, eventStore, sender, defaultSubscriberReceiveMethodName)
		{
			if (0 >= batchSize)
			{
				throw new ArgumentOutOfRangeException("batchSize");
			}
			if (0 >= publishThreadSleep)
			{
				throw new ArgumentOutOfRangeException("publishThreadSleep");
			}

			_batchSize = batchSize;
			_publishThreadSleep = TimeSpan.FromTicks(publishThreadSleep);
		}

		public override void Dispose()
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

				IEventPersisterPosition to;
				foreach (var @event in EventStore.Load(BatchSize, subscription.Value.Position, out to))
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
	}
}
