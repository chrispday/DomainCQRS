using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yeast.EventStore
{
	[Serializable]
	public class MockCommand : ICommand
	{
		public int Increment { get; set; }
		public Guid AggregateRootId { get; set; }
		public int Version { get; set; }
	}

	[Serializable]
	public class MockCommand2
	{
		public int Increment { get; set; }
		public Guid AggregateRootId { get; set; }
		public int Ver { get; set; }
	}
}
