using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS.Test.Mock
{
	public static class EventPublisherConfigure
	{
		public static IConfigure MockEventPublisher(this IConfigure configure)
		{
			configure.Registry
				.BuildInstancesOf<IEventPublisher>()
				.TheDefaultIs(Registry.Instance<IEventPublisher>()
					.UsingConcreteType<MockEventPublisher>()
					.WithProperty("batchSize").EqualTo(100)
					.WithProperty("publishThreadSleep").EqualTo(TimeSpan.FromSeconds(1).Ticks)
					.WithProperty("defaultSubscriberReceiveMethodName").EqualTo("Receive"))
				.AsSingletons();
			return configure;
		}

		public static IConfigure MockEventPublisher(this IConfigure configure, int batchSize, TimeSpan publishThreadSleep)
		{
			configure.Registry
				.BuildInstancesOf<IEventPublisher>()
				.TheDefaultIs(Registry.Instance<IEventPublisher>()
					.UsingConcreteType<MockEventPublisher>()
					.WithProperty("batchSize").EqualTo(batchSize)
					.WithProperty("publishThreadSleep").EqualTo(publishThreadSleep.Ticks)
					.WithProperty("defaultSubscriberReceiveMethodName").EqualTo("Receive"))
				.AsSingletons();
			return configure;
		}
	}

	public class MockEventPublisher : EventPublisher
	{
		public MockEventPublisher(ILogger logger, IEventStore eventStore, int batchSize, long publishThreadSleep, string defaultSubscriberReceiveMethodName)
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
