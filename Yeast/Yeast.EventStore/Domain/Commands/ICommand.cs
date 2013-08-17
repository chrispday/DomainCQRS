using System;
using System.Collections.Generic;

using System.Text;

namespace Yeast.EventStore.Domain
{
	public interface ICommand
	{
		Guid AggregateRootId { get; set; }
	}
}
