using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yeast.EventStore
{
	public interface IAggregateRoot
	{
		Guid AggregateId { get; set; }
		int Version { get; set; }

		IAggregateRoot HandleCommand(object command);
	}

	public interface IAggregateRoot<T> : IAggregateRoot
	{
		new T HandleCommand(object command);
	}
}
