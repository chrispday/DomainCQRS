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
		public static IConfigure MockSyncroEventPublisher(this IConfigure configure)
		{
			configure.Registry
				.BuildInstancesOf<IEventPublisher>()
				.TheDefaultIs(Registry.Instance<IEventPublisher>()
					.UsingConcreteType<MockSynchroEventPublisher>()
					.WithProperty("defaultSubscriberReceiveMethodName").EqualTo("Receive"))
				.AsSingletons();
			return configure;
		}
		public static IConfigure MockBatchEventPublisher(this IConfigure configure, int batchSize, TimeSpan publishThreadSleep)
		{
			configure.Registry
				.BuildInstancesOf<IEventPublisher>()
				.TheDefaultIs(Registry.Instance<IEventPublisher>()
					.UsingConcreteType<MockBatchEventPublisher>()
					.WithProperty("batchSize").EqualTo(batchSize)
					.WithProperty("publishThreadSleep").EqualTo(publishThreadSleep.Ticks)
					.WithProperty("defaultSubscriberReceiveMethodName").EqualTo("Receive"))
				.AsSingletons();
			return configure;
		}
	}

	public class MockSynchroEventPublisher : SynchronousEventPublisher
	{
		public MockSynchroEventPublisher(ILogger logger, IEventStore eventStore, string defaultSubscriberReceiveMethodName)
			: base(logger, eventStore, defaultSubscriberReceiveMethodName)
		{
		}

		public Dictionary<Guid, Tuple<IEventProjector<object>, IEventPersisterPosition>> Subscribers
		{ 
			get 
			{ 
				return _subscribers.ToDictionary(i => i.Key, i => Tuple.Create(i.Value.Subscriber as IEventProjector<object>, i.Value.Position)); 
			} 
		}
	}

	public class MockBatchEventPublisher : BatchEventPublisher
	{
		public MockBatchEventPublisher(ILogger logger, IEventStore eventStore, string defaultSubscriberReceiveMethodName, int batchSize, long publishThreadSleep)
			: base(logger, eventStore, defaultSubscriberReceiveMethodName, batchSize, publishThreadSleep)
		{
		}

		public Dictionary<Guid, Tuple<IEventProjector<object>, IEventPersisterPosition>> Subscribers
		{
			get
			{
				return _subscribers.ToDictionary(i => i.Key, i => Tuple.Create(i.Value.Subscriber as IEventProjector<object>, i.Value.Position));
			}
		}
	}
}
