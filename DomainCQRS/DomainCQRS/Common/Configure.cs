﻿using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	public interface IConfigure : IDisposable
	{
		IMessageReceiver GetMessageReceiver { get; }
		IEventPublisher GetEventPublisher { get; }
		IConfigure Synchrounous();
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

				if (Synchronous)
				{
					MessageReceiver.Synchronous = true;
				}
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

				if (Synchronous)
				{
					EventPublisher.Synchronous = true;
				}
				if (null != MessageReceiver)
				{
					MessageReceiver.EventPublisher = value;
				}
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

		public IMessageReceiver GetMessageReceiver
		{
			get { return MessageReceiver; }
		}

		public IEventPublisher GetEventPublisher
		{
			get { return EventPublisher; }
		}

		public bool Synchronous { get; set; }
		public IConfigure Synchrounous()
		{
			Synchronous = true;

			if (null != MessageReceiver)
			{
				MessageReceiver.Synchronous = true;
				MessageReceiver.EventPublisher = EventPublisher;
			}
			if (null != EventPublisher)
			{
				EventPublisher.Synchronous = true;
			}

			return this;
		}
	}
}