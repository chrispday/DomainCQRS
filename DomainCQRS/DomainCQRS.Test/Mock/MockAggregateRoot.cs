using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainCQRS.Domain;

namespace DomainCQRS.Test
{
	public class MockAggregateRoot : AggregateRootBase, IHandlesCommand<MockCommand>, IHandlesEvent<MockEvent>
	{
		public int Amount { get; set; }

		public MockAggregateRoot()
		{
			Amount = 0;
		}

		public MockAggregateRoot(Guid id) : base(id)
		{
			Amount = 0;
		}

		public IEnumerable Apply(MockCommand command)
		{
			Amount += command.Increment;
			return new object[] { new MockEvent() { AggregateRootId = command.AggregateRootId, Increment = command.Increment } };
		}

		public void Apply(MockEvent @event)
		{
			Amount += @event.Increment;
		}

		public object Apply(MockCommand2 command)
		{
			Amount += command.Increment;
			return new MockEvent() { AggregateRootId = command.Id, Increment = command.Increment };
		}
	}
}
