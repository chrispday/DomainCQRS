using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public interface IConfigure
	{
	}

	public class Configure : IConfigure
	{
		private IEventStoreProvider _eventStoreProvider;
		public IEventStoreProvider EventStoreProvider
		{
			get { return _eventStoreProvider; }
			set
			{
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
			}
		}

		private IEventStore _eventStore;
		public IEventStore EventStore
		{
			get { return _eventStore; }
			set
			{
				_eventStore = value;
				if (null != MessageReceiver)
				{
					MessageReceiver.EventStore = value;
				}
			}
		}

		private IEventSerializer _eventSerializer;
		public IEventSerializer EventSerializer
		{
			get { return _eventSerializer; }
			set
			{
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
			set { _messageReceiver = value; }
		}

		public static IConfigure With()
		{
			return new Configure() { EventStore = new EventStore() };
		}
	}
}
