using System;
using System.Collections.Generic;

using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IConfigure : IDisposable
	{
		IMessageReceiver MessageReceiver { get; }
		IEventPublisher EventPublisher { get; }
	}

	public class Configure : IConfigure
	{
		private IEventStoreProvider _eventStoreProvider;
		public IEventStoreProvider EventStoreProvider
		{
			get { return _eventStoreProvider; }
			set
			{
				if (null == value)
				{
					throw new ArgumentNullException("EventStoreProvider");
				}

				_eventStoreProvider = value;
				if (null != EventStore)
				{
					EventStore.EventStoreProvider = value;
				}
			}
		}

		private ILogger _logger;
		public ILogger Logger
		{
			get { return _logger; }
			set
			{
				if (null == value)
				{
					throw new ArgumentNullException("Logger");
				}

				_logger = value;
				if (null != EventStoreProvider)
				{
					EventStoreProvider.Logger = value;
				}
				if (null != EventStore)
				{
					EventStore.Logger = value;
				}
				if (null != MessageReceiver)
				{
					MessageReceiver.Logger = value;
				}
				if (null != EventPublisher)
				{
					EventPublisher.Logger = value;
				}
			}
		}

		private IEventStore _eventStore;
		public IEventStore EventStore
		{
			get { return _eventStore; }
			set
			{
				if (null == value)
				{
					throw new ArgumentNullException("EventStore");
				}

				_eventStore = value;
				if (null != MessageReceiver)
				{
					MessageReceiver.EventStore = value;
				}
				if (null != EventPublisher)
				{
					EventPublisher.EventStore = value;
				}
			}
		}

		private IEventSerializer _eventSerializer;
		public IEventSerializer EventSerializer
		{
			get { return _eventSerializer; }
			set
			{
				if (null == value)
				{
					throw new ArgumentNullException("EventSerializer");
				}

				_eventSerializer = value;
				if (null != EventStore)
				{
					EventStore.EventSerializer = value;
				}
			}
		}

		private IMessageReceiver _messageReceiver;
		public IMessageReceiver MessageReceiver
		{
			get { return _messageReceiver; }
			set
			{
				if (null == value)
				{
					throw new ArgumentNullException("MessageReceiver");
				}

				_messageReceiver = value;
			}
		}

		private IAggregateRootCache _aggregateRootCache;
		public IAggregateRootCache AggregateRootCache
		{
			get { return _aggregateRootCache; }
			set
			{
				if (null == value)
				{
					throw new ArgumentNullException("AggregateRootCache");
				}

				_aggregateRootCache = value;
				if (null != MessageReceiver)
				{
					MessageReceiver.AggregateRootCache = value;
				}
			}
		}

		private IEventPublisher _eventPublisher;
		public IEventPublisher EventPublisher
		{
			get { return _eventPublisher; }
			set
			{
				if (null == value)
				{
					throw new ArgumentNullException("EventPublisher");
				}

				_eventPublisher = value;
			}
		}

		public static IConfigure With()
		{
			return new Configure();
		}

		public void Dispose()
		{
			if (null != EventPublisher)
			{
				EventPublisher.Dispose();
			}
			if (null != EventStoreProvider)
			{
				EventStoreProvider.Dispose();
			}
		}
	}
}
