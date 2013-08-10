using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

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
		public TimeSpan PublishThreadSleep { get; set; }
		public string DefaultSubscriberReceiveMethodName { get; set; }
		protected delegate void Receive(object subscriber, object @event);
		protected class SubscriberAndPosition
		{
			public object Subscriber;
			public Dictionary<Type, Receive> Receives = new Dictionary<Type,Receive>();
			public Receive ReceiveObject;
			public IEventStoreProviderPosition Position;
		}
		protected Dictionary<Guid, SubscriberAndPosition> _subscribers = new Dictionary<Guid, SubscriberAndPosition>();
		private Thread _publisherThread;
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
		public IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, string subscriberReceiveMethodName)
		{
			SubscriberAndPosition subscriber;
			if (!_subscribers.TryGetValue(subscriptionId, out subscriber))
			{
				_subscribers.Add(subscriptionId, subscriber = new SubscriberAndPosition() { Subscriber = Activator.CreateInstance<Subscriber>(), Position = EventStore.EventStoreProvider.LoadPosition(subscriptionId) });
			}
			if (typeof(object) == typeof(Event))
			{
				subscriber.ReceiveObject = CreateSubscriberReceive<Subscriber, object>(subscriberReceiveMethodName);
			}
			else
			{
				var eventType = typeof(Event);
				if (subscriber.Receives.ContainsKey(eventType))
				{
					throw new RegistrationException(string.Format("{0}({1}) for {2} already registered.", subscriberReceiveMethodName, eventType.Name, subscriptionId));
				}
				subscriber.Receives.Add(eventType, CreateSubscriberReceive<Subscriber, Event>(subscriberReceiveMethodName));
			}

			if (null == _publisherThread)
			{
				_publisherThread = new Thread(Publish);
				_publisherThread.Start();
			}

			return this;
		}

		private Receive CreateSubscriberReceive<Subscriber, Event>(string subscriberReceiveMethodName)
		{
			var subscriberType = typeof(Subscriber);
			var eventType = typeof(Event);
			MethodInfo receiveMethod = subscriberType.GetMethod(subscriberReceiveMethodName, new Type[] { eventType });
			if (null == receiveMethod)
			{
				throw new RegistrationException(string.Format("{0} does not contain a method {1}({2}).", subscriberType.Name, subscriberReceiveMethodName, eventType.Name));
			}

			var dynamicMethod = new DynamicMethod(string.Format("Receive_{0}_{1}", subscriberType.Name, eventType.Name), null, new Type[] { typeof(object), typeof(object) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Castclass, subscriberType);
			ilGenerator.Emit(OpCodes.Ldarg_1);
			ilGenerator.Emit(OpCodes.Castclass, eventType);
			ilGenerator.EmitCall(OpCodes.Callvirt, receiveMethod, null);
			ilGenerator.Emit(OpCodes.Pop);
			ilGenerator.Emit(OpCodes.Ret);

			return (Receive)dynamicMethod.CreateDelegate(typeof(Receive));
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

				var eventsPublished = 0;

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
							Receive receive;
							if (0 == subscription.Value.Receives.Count
								|| !subscription.Value.Receives.TryGetValue(@event.GetType(), out receive))
							{
								receive = subscription.Value.ReceiveObject;
							}

							if (null != receive)
							{
								receive(subscription.Value.Subscriber, @event);
							}

							eventsPublished++;
						}

						EventStore.EventStoreProvider.SavePosition(subscription.Key, subscription.Value.Position = to);
					}
					catch (Exception ex)
					{
						Logger.Error("{0}", ex);
					}
				}

				Logger.Verbose("{0} events published.", eventsPublished);

				Thread.Sleep(PublishThreadSleep);
			}

			Logger.Information("Shutting down publishing thread.");
			_finishedPublishing.Set();
		}
	}
}
