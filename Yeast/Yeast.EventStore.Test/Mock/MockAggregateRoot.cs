using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yeast.EventStore.Test
{
	public class MockAggregateRoot : AggregateRootBase, IHandles<MockCommand>
	{
			public int Amount { get; set; }

			public MockAggregateRoot(IEventStore eventStore)
				: base(eventStore)
			{
				Amount = 0;
			}

			public MockAggregateRoot() : base(null)
			{
				Amount = 0;
			}

			public MockAggregateRoot(IEventStore eventStore, Guid aggregateRootId) : base(eventStore, aggregateRootId) { }

			public IEnumerable Apply(MockCommand @event)
			{
				Amount += @event.Increment;
				return new object[] { };
			}
	}
}
