using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yeast.EventStore.Test.Mock
{
	public static class EventPublisherConfigure
	{
		public static IConfigure MockEventPublisher(this IConfigure configure)
		{
			var c = configure as Configure;
			c.EventPublisher = new MockEventPublisher() { Logger = c.Logger, EventStore = c.EventStore };
			return configure;
		}

		public static IConfigure MockEventPublisher(this IConfigure configure, int batchSize)
		{
			var c = configure as Configure;
			c.EventPublisher = new MockEventPublisher() { Logger = c.Logger, EventStore = c.EventStore, BatchSize = batchSize };
			return configure;
		}
	}

	public class MockEventPublisher : EventPublisher
	{
		public Dictionary<Guid, Tuple<IEventSubscriber, IEventStoreProviderPosition>> Subscribers
		{ 
			get 
			{ 
				return _subscribers.ToDictionary(i => i.Key, i => Tuple.Create(i.Value.Subscriber, i.Value.Position)); 
			} 
		}
	}
}
