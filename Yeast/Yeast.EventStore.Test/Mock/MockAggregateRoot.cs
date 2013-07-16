using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yeast.EventStore.Test
{
	public class MockAggregateRoot : AggregateRootBase, IHandlesCommand<MockCommand>, IHandlesEvent<MockEvent>, IHandlesCommand<MockCommand2>
	{
		public int Amount { get; set; }

		public MockAggregateRoot(IEventStore eventStore)
			: base(eventStore)
		{
			Amount = 0;
		}

		public MockAggregateRoot()
			: base(null)
		{
			Amount = 0;
		}

		public MockAggregateRoot(IEventStore eventStore, Guid aggregateRootId) : base(eventStore, aggregateRootId) { }

		public IEnumerable Apply(MockCommand command)
		{
			Amount += command.Increment;
			return new object[] { new MockEvent() { Increment = command.Increment } };
		}

		public void Apply(MockEvent @event)
		{
			Amount += @event.Increment;
		}

		public IEnumerable Apply(MockCommand2 command)
		{
			Amount += command.Increment;
			return new object[] { new MockEvent() { Increment = command.Increment } };
		}
	}
}
