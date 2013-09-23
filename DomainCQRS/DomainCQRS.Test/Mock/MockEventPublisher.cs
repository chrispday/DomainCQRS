using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainCQRS.Common;

namespace DomainCQRS.Test.Mock
{
	public static class EventPublisherConfigure
	{
		public static IConfigure MockEventPublisher(this IConfigure configure)
		{
			var c = configure as Configure;
			c.EventPublisher = new MockEventPublisher(c.Logger, c.EventStore, 100, TimeSpan.FromSeconds(1), "Receive");
			return configure;
		}

		public static IConfigure MockEventPublisher(this IConfigure configure, int batchSize, TimeSpan publishThreadSleep)
		{
			var c = configure as Configure;
			c.EventPublisher = new MockEventPublisher(c.Logger, c.EventStore, batchSize, publishThreadSleep, "Receive");
			return configure;
		}
	}

	public class MockEventPublisher : EventPublisher
	{
		public MockEventPublisher(ILogger logger, IEventStore eventStore, int batchSize, TimeSpan publishThreadSleep, string defaultSubscriberReceiveMethodName)
			: base(logger, eventStore, batchSize, publishThreadSleep, defaultSubscriberReceiveMethodName)
		{
		}

		public Dictionary<Guid, Tuple<IEventProjector<object>, IEventStoreProviderPosition>> Subscribers
		{ 
			get 
			{ 
				return _subscribers.ToDictionary(i => i.Key, i => Tuple.Create(i.Value.Subscriber as IEventProjector<object>, i.Value.Position)); 
			} 
		}
	}
}
