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
	}

	public class MockSynchroEventPublisher : SynchronousEventPublisher
	{
		public MockSynchroEventPublisher(ILogger logger, IEventStore eventStore, IMessageSender sender, string defaultSubscriberReceiveMethodName)
			: base(logger, eventStore, sender, defaultSubscriberReceiveMethodName)
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
