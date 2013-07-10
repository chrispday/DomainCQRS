using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yeast.EventStore.Test
{
	public class MockAggregateRoot : AggregateRootBase, IApplies<MockCommand>, IHandles<MockCommand>
	{
			public int Amount { get; set; }

			public MockAggregateRoot(IEventStore eventStore) : base(eventStore)
			{
				Amount = 0;
			}

			public MockAggregateRoot(IEventStore eventStore, Guid id) : base(eventStore, id) { }

			public IEnumerable<object> Apply(MockCommand command)
			{
				return new object[] { command };
			}

			public MockCommand When(MockCommand @event)
			{
				Amount += @event.Increment;
				return @event;
			}
	}
}
