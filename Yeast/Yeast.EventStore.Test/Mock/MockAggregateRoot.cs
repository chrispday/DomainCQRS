using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yeast.EventStore.Test
{
	public class MockAggregateRoot : AggregateRootBase<MockAggregateRoot>, IHandles<MockCommand>
	{
			public int Amount { get; set; }

			public MockAggregateRoot()
			{
				Amount = 0;
			}

			public MockAggregateRoot(Guid id) : base(id) { }

			public void Handle(MockCommand command)
			{
				Amount += command.Increment;
			}
	}
}
